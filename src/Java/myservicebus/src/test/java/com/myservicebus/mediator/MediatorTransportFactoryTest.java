package com.myservicebus.mediator;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.di.ServiceCollection;
import java.util.concurrent.CompletableFuture;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

public class MediatorTransportFactoryTest {

    public static class TestMessage {
        private final String value;
        public TestMessage(String value) { this.value = value; }
        public String getValue() { return value; }
    }

    public static class TestConsumer implements Consumer<TestMessage> {
        static CompletableFuture<TestMessage> received = new CompletableFuture<>();
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            received.complete(context.getMessage());
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    public void publishDeliversMessageToConsumer() {
        ServiceCollection services = new ServiceCollection();
        MediatorBus bus = MediatorBus.configure(services, cfg -> {
            cfg.addConsumer(TestConsumer.class);
        });

        TestConsumer.received = new CompletableFuture<>();
        bus.publish(new TestMessage("hello"));

        Assertions.assertEquals("hello", TestConsumer.received.join().getValue());
    }
}
