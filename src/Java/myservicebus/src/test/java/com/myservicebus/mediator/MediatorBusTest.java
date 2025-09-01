package com.myservicebus.mediator;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.di.ServiceCollection;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

public class MediatorBusTest {

    public static class TestMessage {
        private final String value;
        public TestMessage(String value) { this.value = value; }
        public String getValue() { return value; }
    }

    public static class TestConsumer implements Consumer<TestMessage> {
        static AtomicReference<String> received = new AtomicReference<>();
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            received.set(context.getMessage().getValue());
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    public void publish_delivers_message_to_consumer() {
        ServiceCollection services = new ServiceCollection();
        MediatorBus bus = MediatorBus.configure(services, cfg -> {
            cfg.addConsumer(TestConsumer.class);
        });

        TestConsumer.received.set(null);
        bus.publish(new TestMessage("hello"));

        Assertions.assertEquals("hello", TestConsumer.received.get());
    }
}
