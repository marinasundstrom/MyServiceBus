package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.tasks.CancellationToken;

class ConsumerMessageFilterTest {
    static class TestMessage {
        private String text;

        TestMessage(String text) {
            this.text = text;
        }

        public String getText() {
            return text;
        }
    }

    static class FaultingConsumer implements Consumer<TestMessage> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            return CompletableFuture.failedFuture(new RuntimeException("boom"));
        }
    }

    static class CaptureEndpoint implements SendEndpoint {
        Object sent;

        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            this.sent = message;
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    void sendsFaultWhenConsumerThrows() {
        ServiceCollection services = new ServiceCollection();
        services.addScoped(FaultingConsumer.class);
        services.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        ServiceProvider provider = services.buildServiceProvider();

        CaptureEndpoint endpoint = new CaptureEndpoint();
        SendEndpointProvider sendProvider = uri -> endpoint;

        UUID id = UUID.randomUUID();
        ConsumeContext<TestMessage> ctx = new ConsumeContext<>(
                new TestMessage("hi"),
                Map.of("messageId", id),
                "queue",
                null,
                CancellationToken.none,
                sendProvider);

        PipeConfigurator<ConsumeContext<TestMessage>> configurator = new PipeConfigurator<>();
        configurator.useFilter(new ConsumerFaultFilter<>(provider, FaultingConsumer.class));
        configurator.useRetry(1);
        ConsumerFactory factory = new ScopeConsumerFactory(provider);
        configurator.useFilter(new ConsumerMessageFilter<>(FaultingConsumer.class, factory));
        Pipe<ConsumeContext<TestMessage>> pipe = configurator.build();

        CompletableFuture<Void> future = pipe.send(ctx);
        assertThrows(RuntimeException.class, future::join);

        assertTrue(endpoint.sent instanceof Fault<?>);
        @SuppressWarnings("unchecked")
        Fault<TestMessage> fault = (Fault<TestMessage>) endpoint.sent;
        assertEquals("boom", fault.getExceptions().get(0).getMessage());
        assertEquals(id, fault.getMessageId());
    }
}
