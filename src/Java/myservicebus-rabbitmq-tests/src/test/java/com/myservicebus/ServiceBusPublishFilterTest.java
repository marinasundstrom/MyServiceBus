package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicBoolean;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.rabbitmq.ConnectionProvider;
import com.rabbitmq.client.ConnectionFactory;

class ServiceBusPublishFilterTest {
    @Test
    void publish_executes_send_and_publish_filters() throws Exception {
        AtomicBoolean sendCalled = new AtomicBoolean(false);
        AtomicBoolean publishCalled = new AtomicBoolean(false);

        PipeConfigurator<SendContext> sendCfg = new PipeConfigurator<>();
        sendCfg.useExecute(ctx -> { sendCalled.set(true); return CompletableFuture.completedFuture(null); });
        PipeConfigurator<SendContext> publishCfg = new PipeConfigurator<>();
        publishCfg.useExecute(ctx -> { publishCalled.set(true); return CompletableFuture.completedFuture(null); });

        ServiceCollection services = new ServiceCollection();
        services.addSingleton(SendPipe.class, sp -> () -> new SendPipe(sendCfg.build()));
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(publishCfg.build()));
        services.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        services.addSingleton(SendEndpointProvider.class,
                sp -> () -> new SendEndpointProviderImpl(
                        sp.getService(ConsumeContextProvider.class),
                        sp.getService(TransportSendEndpointProvider.class)));
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> uri -> new SendEndpoint() {
            @Override
            public CompletableFuture<Void> send(SendContext ctx) {
                return sp.getService(SendPipe.class).send(ctx);
            }

            @Override
            public <T> CompletableFuture<Void> send(T message,
                    com.myservicebus.tasks.CancellationToken cancellationToken) {
                return send(new SendContext(message, cancellationToken));
            }
        });
        services.addSingleton(ConnectionProvider.class, sp -> () -> new ConnectionProvider(new ConnectionFactory()));

        ServiceBus bus = new ServiceBus(services.build());

        bus.publish("hi");

        assertTrue(sendCalled.get());
        assertTrue(publishCalled.get());
    }
}
