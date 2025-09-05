package com.myservicebus.mediator;

import com.myservicebus.*;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.TopologyRegistry;
import java.util.concurrent.CompletableFuture;
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
            if (attempts < 3)
                return CompletableFuture.failedFuture(new RuntimeException("boom"));
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    void usesRetryFilterToInvokeConsumer() {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(RetryConsumer.class);
        services.addScoped(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());

        TopologyRegistry registry = new TopologyRegistry();
        registry.registerConsumer(RetryConsumer.class, "queue", null, TestMessage.class);
        services.addSingleton(TopologyRegistry.class, sp -> () -> registry);

        ServiceProvider provider = services.buildServiceProvider();
        MediatorSendEndpoint endpoint = new MediatorSendEndpoint(provider, new MediatorSendEndpointProvider(provider));

        RetryConsumer.attempts = 0;
        endpoint.send(new TestMessage(), CancellationToken.none).join();

        Assertions.assertEquals(3, RetryConsumer.attempts);
    }
}

