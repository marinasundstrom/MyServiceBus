package com.myservicebus;

import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

class IntegrationPolicyTest {
    record LocalOrder(int quantity) { }
    public static class OrderConsumer implements Consumer<LocalOrder> {
        static int attempts;
        public java.util.concurrent.CompletableFuture<Void> consume(ConsumeContext<LocalOrder> context) {
            assertNotNull(context.getMessageId());
            return ++attempts == 1
                    ? java.util.concurrent.CompletableFuture.failedFuture(new IllegalStateException("retry"))
                    : java.util.concurrent.CompletableFuture.completedFuture(null);
        }
    }

    @Test
    void startupAndDefinitionPolicyUseConfiguredContract() throws Exception {
        OrderConsumer.attempts = 0;
        var services = com.myservicebus.di.ServiceCollection.create();
        services.addSingleton(TransportFactory.class, ignored -> () -> new TransportFactory() {
            java.util.function.Function<TransportMessage, java.util.concurrent.CompletableFuture<Void>> receiver;
            public SendTransport getSendTransport(java.net.URI address) {
                return (body, headers, contentType) -> receiver.apply(new TransportMessage(body, headers)).join();
            }
            public ReceiveTransport createReceiveTransport(com.myservicebus.topology.ReceiveEndpointTransportTopology topology,
                    java.util.function.Function<TransportMessage, java.util.concurrent.CompletableFuture<Void>> handler,
                    java.util.function.Function<String, Boolean> registered) {
                assertEquals(2, topology.concurrentMessageLimit());
                receiver = handler;
                return new ReceiveTransport() { public void start() { } public void stop() { } };
            }
            public String getPublishAddress(String entity) { return "loopback://" + entity; }
            public String getSendAddress(String queue) { return "loopback://" + queue; }
        });
        var bus = MessageBusImpl.configure(services, cfg -> {
            cfg.addConsumer(OrderConsumer.class, new ConsumerDefinition<OrderConsumer>()
                    .endpointName("orders").concurrentMessageLimit(2).configurePipeline(pipe -> pipe.useRetry(1)));
            com.myservicebus.mediator.MediatorTransport.configure(cfg);
        });
        try {
            bus.start();
            bus.start();
            bus.publish(LocalOrder.class, new LocalOrder(3)).join();
            assertEquals(2, OrderConsumer.attempts);
        } finally { bus.stop(); }
    }

    static class BaseOrder { }
    static class DerivedOrder extends BaseOrder { }
    @Test
    void classTokenPreservesTheSelectedContract() {
        var captured = new java.util.concurrent.atomic.AtomicReference<PublishContext>();
        PublishEndpoint endpoint = new PublishEndpoint() {
            public <T> java.util.concurrent.CompletableFuture<Void> publish(T message, com.myservicebus.tasks.CancellationToken token) {
                throw new AssertionError("The configured context should be preserved");
            }
            public java.util.concurrent.CompletableFuture<Void> publish(PublishContext context) {
                captured.set(context);
                return java.util.concurrent.CompletableFuture.completedFuture(null);
            }
        };
        endpoint.publish(BaseOrder.class, new DerivedOrder()).join();
        assertEquals(BaseOrder.class, captured.get().getContractType());
    }
}
