package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.net.URI;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.ArrayList;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.myservicebus.TransportMessage;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.rabbitmq.ConnectionProvider;
import com.rabbitmq.client.ConnectionFactory;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.serialization.MessageSerializer;

class ServiceBusPublishFilterTest {
    @Test
    void publish_executes_send_and_publish_filters() throws Exception {
        List<String> calls = new ArrayList<>();

        PipeConfigurator<SendContext> sendCfg = new PipeConfigurator<>();
        sendCfg.useFilter((context, next) -> {
            calls.add("send:before");
            return next.send(context).thenRun(() -> calls.add("send:after"));
        });
        PipeConfigurator<PublishContext> publishCfg = new PipeConfigurator<>();
        publishCfg.useFilter((context, next) -> {
            calls.add("publish:before");
            return next.send(context).thenRun(() -> calls.add("publish:after"));
        });

        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(SendPipe.class, sp -> () -> new SendPipe(sendCfg.build()));
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(publishCfg.build()));
        services.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        services.addSingleton(SendEndpointProvider.class,
                sp -> () -> new SendEndpointProviderImpl(
                        sp.getService(ConsumeContextProvider.class),
                        sp.getService(TransportSendEndpointProvider.class)));
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> new TransportSendEndpointProvider() {
            @Override
            public SendEndpoint getSendEndpoint(String uri) {
                return new SendEndpoint() {
                    @Override
                    public CompletableFuture<Void> send(SendContext ctx) {
                        return sp.getService(SendPipe.class).send(ctx)
                                .thenRun(() -> calls.add("transport"));
                    }

                    @Override
                    public <T> CompletableFuture<Void> send(T message,
                            com.myservicebus.tasks.CancellationToken cancellationToken) {
                        return send(new SendContext(message, cancellationToken));
                    }
                };
            }

            @Override
            public TransportSendEndpointProvider withSerializer(MessageSerializer serializer) {
                return this;
            }
        });
        services.addSingleton(ConnectionProvider.class, sp -> () -> new ConnectionProvider(new ConnectionFactory()));
        services.addSingleton(TransportFactory.class, sp -> () -> new TransportFactory() {
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
                throw new UnsupportedOperationException();
            }

            @Override
            public String getPublishAddress(String exchange) {
                return "rabbitmq://localhost/exchange/" + exchange;
            }

            @Override
            public String getSendAddress(String queue) {
                return "rabbitmq://localhost/" + queue;
            }
        });

        MessageBus bus = new MessageBusImpl(services.buildServiceProvider());

        bus.start();
        try {
            bus.publish("hi").join();
        } finally {
            bus.stop();
        }

        assertEquals(
                List.of("publish:before", "publish:after", "send:before", "send:after", "transport"),
                calls);
    }
}
