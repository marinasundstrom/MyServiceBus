package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;
import java.net.URI;
import java.util.concurrent.CopyOnWriteArrayList;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.mediator.MediatorTransport;

class BusHookTest {
    public record TestMessage(String value) {
    }

    public static final class RecordingHook implements BusHook {
        static final List<BusHookEvent> EVENTS = new CopyOnWriteArrayList<>();

        @Override
        public void handle(BusHookEvent busEvent) {
            EVENTS.add(busEvent);
        }
    }

    public static final class ThrowingHook implements BusHook {
        @Override
        public void handle(BusHookEvent busEvent) {
            throw new IllegalStateException("Hook failure");
        }
    }

    @Test
    void registeredHooksObserveLifecycleAndMessageOperations() throws Exception {
        RecordingHook.EVENTS.clear();
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(TransportFactory.class, ignored -> () -> new NoOpTransportFactory());
        MessageBus bus = MessageBusImpl.configure(services, configurator -> {
            configurator.addHook(RecordingHook.class);
            MediatorTransport.configure(configurator);
        });

        bus.start();
        bus.publish(new TestMessage("hello")).join();
        bus.stop();

        assertTrue(RecordingHook.EVENTS.stream()
                .anyMatch(event -> event instanceof BusLifecycleHookEvent lifecycle && lifecycle.state().equals("started")));
        assertTrue(RecordingHook.EVENTS.stream()
                .anyMatch(event -> event instanceof BusLifecycleHookEvent lifecycle && lifecycle.state().equals("stopped")));
        assertTrue(RecordingHook.EVENTS.stream()
                .anyMatch(event -> event instanceof MessageOperationHookEvent operation && operation.kind().equals("published")));
    }

    @Test
    void hookFailuresDoNotChangeMessageOutcomes() throws Exception {
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(TransportFactory.class, ignored -> () -> new NoOpTransportFactory());
        MessageBus bus = MessageBusImpl.configure(services, configurator -> {
            configurator.addHook(ThrowingHook.class);
            MediatorTransport.configure(configurator);
        });

        bus.start();
        assertDoesNotThrow(() -> bus.publish(new TestMessage("hello")).join());
        bus.stop();
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
}
