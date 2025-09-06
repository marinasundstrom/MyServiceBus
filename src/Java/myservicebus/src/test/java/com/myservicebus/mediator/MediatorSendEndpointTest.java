package com.myservicebus.mediator;

import com.myservicebus.*;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.TopologyRegistry;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

class MediatorSendEndpointTest {
    static class TestMessage {
    }

    static class RetryConsumer implements Consumer<TestMessage> {
        static int attempts;

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            attempts++;
            if (attempts < 2)
                return CompletableFuture.failedFuture(new RuntimeException("boom"));
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    void doesNotRetryByDefault() {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(RetryConsumer.class);
        services.addScoped(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());

        TopologyRegistry registry = new TopologyRegistry();
        registry.registerConsumer(RetryConsumer.class, "queue", null, TestMessage.class);
        services.addSingleton(TopologyRegistry.class, sp -> () -> registry);

        ServiceProvider provider = services.buildServiceProvider();
        MediatorSendEndpoint endpoint = new MediatorSendEndpoint(provider, new MediatorSendEndpointProvider(provider));

        RetryConsumer.attempts = 0;
        CompletableFuture<Void> future = endpoint.send(new TestMessage(), CancellationToken.none);
        Assertions.assertThrows(CompletionException.class, future::join);
        Assertions.assertEquals(1, RetryConsumer.attempts);
    }

    @Test
    void retriesWhenConfigured() {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(RetryConsumer.class);
        services.addScoped(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());

        TopologyRegistry registry = new TopologyRegistry();
        registry.registerConsumer(RetryConsumer.class, "queue", cfg -> cfg.useRetry(2), TestMessage.class);
        services.addSingleton(TopologyRegistry.class, sp -> () -> registry);

        ServiceProvider provider = services.buildServiceProvider();
        MediatorSendEndpoint endpoint = new MediatorSendEndpoint(provider, new MediatorSendEndpointProvider(provider));

        RetryConsumer.attempts = 0;
        endpoint.send(new TestMessage(), CancellationToken.none).join();
        Assertions.assertEquals(2, RetryConsumer.attempts);
    }
}
