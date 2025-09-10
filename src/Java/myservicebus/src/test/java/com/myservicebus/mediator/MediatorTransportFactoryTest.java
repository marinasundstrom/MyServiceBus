package com.myservicebus.mediator;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.Handler;
import com.myservicebus.HandlerWithResult;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.tasks.CancellationTokenSource;
import com.myservicebus.di.ServiceCollection;
import org.junit.jupiter.api.Disabled;
import java.util.Map;
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

    public static class TestHandler implements Handler<TestMessage> {
        static CompletableFuture<TestMessage> received = new CompletableFuture<>();

        @Override
        public CompletableFuture<Void> handle(TestMessage message, CancellationToken cancellationToken) {
            received.complete(message);
            return CompletableFuture.completedFuture(null);
        }
    }

    public static class RequestMessage {
        private final String value;

        public RequestMessage(String value) {
            this.value = value;
        }

        public String getValue() {
            return value;
        }
    }

    public static class ResponseMessage {
        private final String value;

        public ResponseMessage(String value) {
            this.value = value;
        }

        public String getValue() {
            return value;
        }
    }

    public static class ResultHandler implements HandlerWithResult<RequestMessage, ResponseMessage> {
        static CompletableFuture<CancellationToken> token = new CompletableFuture<>();

        @Override
        public CompletableFuture<ResponseMessage> handle(RequestMessage message, CancellationToken cancellationToken) {
            token.complete(cancellationToken);
            return CompletableFuture.completedFuture(new ResponseMessage(message.getValue() + "-response"));
        }
    }

    static class CapturingSendEndpoint implements SendEndpoint {
        static CompletableFuture<Object> sent = new CompletableFuture<>();

        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            sent.complete(message);
            return CompletableFuture.completedFuture(null);
        }
    }

    static class CapturingProvider implements SendEndpointProvider {
        private final SendEndpoint endpoint = new CapturingSendEndpoint();

        @Override
        public SendEndpoint getSendEndpoint(String uri) {
            return endpoint;
        }
    }

    @Test
    @Disabled("Scope setup under development")
    public void publishDeliversMessageToConsumer() {
        ServiceCollection services = ServiceCollection.create();
        MediatorBus bus = MediatorBus.configure(services, cfg -> {
            cfg.addConsumer(TestConsumer.class);
        });

        TestConsumer.received = new CompletableFuture<>();
        bus.publish(new TestMessage("hello"));

        Assertions.assertEquals("hello", TestConsumer.received.join().getValue());
    }

    @Test
    @Disabled("Scope setup under development")
    public void publishDeliversMessageToHandler() {
        ServiceCollection services = ServiceCollection.create();
        MediatorBus bus = MediatorBus.configure(services, cfg -> {
            cfg.addConsumer(TestHandler.class);
        });

        TestHandler.received = new CompletableFuture<>();
        bus.publish(new TestMessage("handler"));

        Assertions.assertEquals("handler", TestHandler.received.join().getValue());
    }

    @Test
    public void handlerWithResultResponds() throws Exception {
        ResultHandler.token = new CompletableFuture<>();
        CapturingSendEndpoint.sent = new CompletableFuture<>();

        CancellationTokenSource cts = new CancellationTokenSource();
        ConsumeContext<RequestMessage> context = new ConsumeContext<>(
                new RequestMessage("hi"),
                Map.of(),
                "queue:response",
                null,
                cts.getToken(),
                new CapturingProvider());

        new ResultHandler().consume(context).join();

        ResponseMessage response = (ResponseMessage) CapturingSendEndpoint.sent.join();
        Assertions.assertEquals("hi-response", response.getValue());
        Assertions.assertEquals(cts.getToken(), ResultHandler.token.join());
    }
}
