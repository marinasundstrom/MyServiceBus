package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import java.net.URI;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.Function;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.assertEquals;

class BusInitializationTest {
    record LifecycleOrder(String id) { }
    public static class LifecycleConsumer implements Consumer<LifecycleOrder> {
        static int deliveries;
        public CompletableFuture<Void> consume(ConsumeContext<LifecycleOrder> context) {
            deliveries++;
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    void standaloneStartInitializesConsumersOnceAcrossRestart() throws Exception {
        LifecycleConsumer.deliveries = 0;
        var starts = new AtomicInteger();
        var factory = new TransportFactory() {
            Function<TransportMessage, CompletableFuture<Void>> receiver;
            public SendTransport getSendTransport(URI address) {
                return (body, headers, contentType) -> receiver.apply(new TransportMessage(body, headers)).join();
            }
            public String getPublishAddress(String entity) { return "loopback://" + entity; }
            public String getSendAddress(String queue) { return "loopback://" + queue; }
            public ReceiveTransport createReceiveTransport(com.myservicebus.topology.ReceiveEndpointTransportTopology topology,
                    Function<TransportMessage, CompletableFuture<Void>> handler, Function<String, Boolean> registered) {
                receiver = handler;
                return new ReceiveTransport() {
                    public void start() { starts.incrementAndGet(); }
                    public void stop() { }
                };
            }
        };
        var services = ServiceCollection.create();
        services.addSingleton(TransportFactory.class, ignored -> () -> factory);
        var bus = MessageBusImpl.configure(services, cfg -> {
            cfg.addConsumer(LifecycleConsumer.class);
            com.myservicebus.mediator.MediatorTransport.configure(cfg);
        });
        try {
            bus.start();
            bus.start();
            assertEquals(1, starts.get());
            bus.publish(new LifecycleOrder("first")).join();
            bus.stop();
            bus.start();
            bus.publish(new LifecycleOrder("second")).join();
            assertEquals(2, starts.get());
            assertEquals(2, LifecycleConsumer.deliveries);
        } finally { bus.stop(); }
    }
}
