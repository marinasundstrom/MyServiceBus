package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.net.URI;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.myservicebus.MessageBusImpl;
import com.myservicebus.NamingConventions;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.topology.MessageBinding;

class ErrorQueueTest {
    static class MyMessage { }

    @Test
    void sendsFaultedMessagesToErrorQueue() throws Exception {
        List<SendContext> errorMessages = new ArrayList<>();
        StubFactory factory = new StubFactory();

        ServiceCollection services = new ServiceCollection();
        services.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        services.addSingleton(SendPipe.class, sp -> () -> new SendPipe(ctx -> CompletableFuture.completedFuture(null)));
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(ctx -> CompletableFuture.completedFuture(null)));
        services.addSingleton(SendEndpointProvider.class,
                sp -> () -> new SendEndpointProviderImpl(sp.getService(ConsumeContextProvider.class),
                        sp.getService(TransportSendEndpointProvider.class)));
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> uri -> new SendEndpoint() {
            @Override
            public CompletableFuture<Void> send(SendContext ctx) {
                if (uri.equals(factory.errorAddress)) {
                    errorMessages.add(ctx);
                }
                return CompletableFuture.completedFuture(null);
            }

            @Override
            public <T> CompletableFuture<Void> send(T message, com.myservicebus.tasks.CancellationToken cancellationToken) {
                return send(new SendContext(message, cancellationToken));
            }
        });
        services.addSingleton(TransportFactory.class, sp -> () -> factory);

        ServiceProvider provider = services.buildServiceProvider();
        MessageBusImpl bus = new MessageBusImpl(provider);

        bus.addHandler("input", MyMessage.class, "input", ctx -> {
            return CompletableFuture.failedFuture(new RuntimeException("boom"));
        }, null, null, null);

        // simulate message arrival
        Map<String, Object> headers = new HashMap<>();
        headers.put(MessageHeaders.FAULT_ADDRESS, "rabbitmq://localhost/exchange/custom_fault");
        Envelope<MyMessage> envelope = new Envelope<>();
        envelope.setMessage(new MyMessage());
        envelope.setHeaders(new HashMap<>());
        envelope.setMessageType(List.of(NamingConventions.getMessageUrn(MyMessage.class)));
        ObjectMapper mapper = new ObjectMapper();
        mapper.disable(SerializationFeature.FAIL_ON_EMPTY_BEANS);
        byte[] body = mapper.writeValueAsBytes(envelope);
        try {
            factory.handler.apply(new TransportMessage(body, headers)).join();
        } catch (Exception ignored) {
        }

        assertEquals(1, errorMessages.size());
        Map<String, Object> errorHeaders = errorMessages.get(0).getHeaders();
        assertEquals(RuntimeException.class.getName(), errorHeaders.get(MessageHeaders.EXCEPTION_TYPE));
        assertEquals("fault", errorHeaders.get(MessageHeaders.REASON));
        assertEquals(0, errorHeaders.get(MessageHeaders.REDELIVERY_COUNT));
        assertTrue(errorHeaders.containsKey(MessageHeaders.HOST_MACHINE));
        assertTrue(errorHeaders.containsKey(MessageHeaders.HOST_PROCESS));
    }

    static class StubFactory implements TransportFactory {
        Function<TransportMessage, CompletableFuture<Void>> handler;
        String errorAddress;

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
