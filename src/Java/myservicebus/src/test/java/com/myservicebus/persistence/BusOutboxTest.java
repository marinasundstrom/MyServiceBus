package com.myservicebus.persistence;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusServices;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.SendTransport;
import com.myservicebus.TransportFactory;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.mediator.MediatorTransport;
import com.myservicebus.tasks.CancellationToken;
import java.time.Duration;
import java.net.URI;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import org.junit.jupiter.api.Test;

class BusOutboxTest {
    @Test
    void scopedPublishAndSendAreCapturedByActiveOutboxSession() throws Exception {
        UUID publishCorrelationId = UUID.randomUUID();
        UUID sendCorrelationId = UUID.randomUUID();
        ServiceCollection services = configuredServices();
        ServiceProvider provider = services.buildServiceProvider();
        MessageBus bus = provider.getRequiredService(MessageBus.class);
        bus.start();

        try (ServiceScope scope = provider.createScope()) {
            ServiceProvider scoped = scope.getServiceProvider();
            RecordingOutboxWriter writer = new RecordingOutboxWriter();
            PublishEndpoint publishEndpoint = scoped.getRequiredService(PublishEndpoint.class);
            SendEndpointProvider endpointProvider = scoped.getRequiredService(SendEndpointProvider.class);
            SendEndpoint sendEndpoint = endpointProvider.getSendEndpoint("loopback://localhost/orders");
            try (OutboxSession.Registration ignored = scoped.getRequiredService(OutboxSession.class).begin(writer)) {
                bus.publish(new DirectBusMessage(UUID.randomUUID())).join();

                publishEndpoint.publish(new OrderSubmitted(UUID.randomUUID()), context ->
                        context.setCorrelationId(publishCorrelationId)).join();

                sendEndpoint.send(new SubmitOrder(UUID.randomUUID()), context ->
                        context.setCorrelationId(sendCorrelationId)).join();
            }
            publishEndpoint.publish(new DirectBusMessage(UUID.randomUUID())).join();

            assertEquals(2, writer.messages.size());
            assertEquals(OutboxDeliveryIntent.PUBLISH, writer.messages.get(0).intent());
            assertEquals(publishCorrelationId, writer.messages.get(0).correlationId());
            assertEquals(OutboxDeliveryIntent.SEND, writer.messages.get(1).intent());
            assertEquals(sendCorrelationId, writer.messages.get(1).correlationId());
            assertEquals("loopback://localhost/orders", writer.messages.get(1).destinationAddress().toString());
        } finally {
            bus.stop();
        }
    }

    @Test
    void nestedOutboxSessionsAreRejected() {
        OutboxSession session = new OutboxSession();
        try (OutboxSession.Registration ignored = session.begin(new RecordingOutboxWriter())) {
            IllegalStateException failure = assertThrows(
                    IllegalStateException.class,
                    () -> session.begin(new RecordingOutboxWriter()));
            assertTrue(failure.getMessage().contains("already active"));
        }
    }

    @Test
    void scheduledMessagesAreRejectedWhileOutboxSessionIsActive() throws Exception {
        ServiceCollection services = configuredServices();
        ServiceProvider provider = services.buildServiceProvider();
        MessageBus bus = provider.getRequiredService(MessageBus.class);
        bus.start();

        try (ServiceScope scope = provider.createScope()) {
            ServiceProvider scoped = scope.getServiceProvider();
            try (OutboxSession.Registration ignored = scoped.getRequiredService(OutboxSession.class)
                    .begin(new RecordingOutboxWriter())) {
                PublishEndpoint endpoint = scoped.getRequiredService(PublishEndpoint.class);

                CompletionException failure = assertThrows(CompletionException.class, () ->
                        endpoint.publish(new OrderSubmitted(UUID.randomUUID()), context ->
                                context.setScheduledEnqueueTime(Duration.ofMinutes(1))).join());

                assertTrue(failure.getCause() instanceof UnsupportedOperationException);
                assertTrue(failure.getCause().getMessage().contains("transactional outbox"));
            }
        } finally {
            bus.stop();
        }
    }

    private static ServiceCollection configuredServices() {
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(TransportFactory.class, ignored -> () -> new NoOpTransportFactory());
        services.from(MessageBusServices.class).addServiceBus(configurator -> {
            configurator.useBusOutbox();
            MediatorTransport.configure(configurator);
        });
        return services;
    }

    private static final class NoOpTransportFactory implements TransportFactory {
        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> {
            };
        }

        @Override
        public String getPublishAddress(String exchange) {
            return "loopback://" + exchange;
        }

        @Override
        public String getSendAddress(String queue) {
            return "loopback://" + queue;
        }
    }

    private static final class RecordingOutboxWriter implements OutboxWriter {
        private final List<OutboxMessage> messages = new ArrayList<>();

        @Override
        public CompletableFuture<Void> add(OutboxMessage message, CancellationToken cancellationToken) {
            messages.add(message);
            return CompletableFuture.completedFuture(null);
        }
    }

    private record OrderSubmitted(UUID orderId) {
    }

    private record SubmitOrder(UUID orderId) {
    }

    private record DirectBusMessage(UUID id) {
    }
}
