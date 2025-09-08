package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.net.URI;
import java.util.*;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.serialization.MessageSerializer;

class FaultQueueTest {
    static class MyMessage { }

    @Test
    void publishesFaultToFaultQueueByDefault() throws Exception {
        List<Object> faultMessages = new ArrayList<>();
        List<SendContext> errorMessages = new ArrayList<>();
        StubFactory factory = new StubFactory();

        ServiceCollection services = new ServiceCollection();
        services.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        services.addSingleton(SendPipe.class, sp -> () -> new SendPipe(ctx -> CompletableFuture.completedFuture(null)));
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(ctx -> CompletableFuture.completedFuture(null)));
        services.addSingleton(SendEndpointProvider.class,
                sp -> () -> new SendEndpointProviderImpl(sp.getService(ConsumeContextProvider.class),
                        sp.getService(TransportSendEndpointProvider.class)));
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> new TransportSendEndpointProvider() {
            @Override
            public SendEndpoint getSendEndpoint(String uri) {
                return new SendEndpoint() {
                    @Override
                    public CompletableFuture<Void> send(SendContext ctx) {
                        if (uri.equals(factory.errorAddress)) {
                            errorMessages.add(ctx);
                        } else if (uri.equals(factory.faultAddress)) {
                            faultMessages.add(ctx.getMessage());
                        }
                        return CompletableFuture.completedFuture(null);
                    }

                    @Override
                    public <T> CompletableFuture<Void> send(T message, com.myservicebus.tasks.CancellationToken cancellationToken) {
                        return send(new SendContext(message, cancellationToken));
                    }
                };
            }

            @Override
            public TransportSendEndpointProvider withSerializer(MessageSerializer serializer) {
                return this;
            }
        });
        services.addSingleton(TransportFactory.class, sp -> () -> factory);

        ServiceProvider provider = services.buildServiceProvider();
        MessageBusImpl bus = new MessageBusImpl(provider);

        bus.addHandler("input", MyMessage.class, "input", ctx -> {
            return CompletableFuture.failedFuture(new RuntimeException("boom"));
        }, null, null, null, null, null);

        Map<String, Object> headers = new HashMap<>();
        Envelope<MyMessage> envelope = new Envelope<>();
        envelope.setMessage(new MyMessage());
        envelope.setHeaders(new HashMap<>());
        envelope.setMessageType(List.of(NamingConventions.getMessageUrn(MyMessage.class)));
        ObjectMapper mapper = new ObjectMapper();
        mapper.disable(SerializationFeature.FAIL_ON_EMPTY_BEANS);
        byte[] body = mapper.writeValueAsBytes(envelope);
        try {
            headers.put(MessageHeaders.FAULT_ADDRESS, factory.faultAddress);
            factory.handler.apply(new TransportMessage(body, headers)).join();
        } catch (Exception ignored) {
        }

        assertEquals(1, errorMessages.size());
        assertEquals(1, faultMessages.size());
        assertTrue(faultMessages.get(0) instanceof Fault);
    }

    static class StubFactory implements TransportFactory {
        Function<TransportMessage, CompletableFuture<Void>> handler;
        String errorAddress;
        String faultAddress;

        @Override
        public SendTransport getSendTransport(URI address) {
            return new SendTransport() {
                @Override
                public void send(byte[] data, Map<String, Object> headers, String contentType) {
                }
            };
        }

        @Override
        public ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
                Function<TransportMessage, CompletableFuture<Void>> handler) {
            this.handler = handler;
            this.errorAddress = getPublishAddress(queueName + "_error");
            this.faultAddress = getPublishAddress(queueName + "_fault");
            return new ReceiveTransport() {
                @Override
                public void start() {
                }

                @Override
                public void stop() {
                }
            };
        }

        @Override
        public String getPublishAddress(String exchange) {
            return "rabbitmq://localhost/exchange/" + exchange;
        }

        @Override
        public String getSendAddress(String queue) {
            return "rabbitmq://localhost/" + queue;
        }
    }
}
