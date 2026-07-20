package com.myservicebus.mediator;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.Filter;
import com.myservicebus.Handler;
import com.myservicebus.HandlerWithResult;
import com.myservicebus.Pipe;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.tasks.CancellationTokenSource;
import com.myservicebus.di.ServiceCollection;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import javax.inject.Inject;
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

    public static class RetryingConsumer implements Consumer<TestMessage> {
        static int attempts;
        static List<String> calls = new ArrayList<>();

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            calls.add("consumer:" + ++attempts);
            return attempts == 1
                    ? CompletableFuture.failedFuture(new IllegalStateException("retry"))
                    : CompletableFuture.completedFuture(null);
        }
    }

    public static class FailingConsumer implements Consumer<TestMessage> {
        static int attempts;
        static List<String> calls = new ArrayList<>();

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            calls.add("consumer:" + ++attempts);
            return CompletableFuture.failedFuture(new IllegalStateException("exhausted"));
        }
    }

    static class RecordingConsumeFilter implements Filter<ConsumeContext<TestMessage>> {
        private final String name;
        private final List<String> calls;

        RecordingConsumeFilter(String name, List<String> calls) {
            this.name = name;
            this.calls = calls;
        }

        @Override
        public CompletableFuture<Void> send(ConsumeContext<TestMessage> context, Pipe<ConsumeContext<TestMessage>> next) {
            calls.add(name + ":before");
            return next.send(context).whenComplete((ignored, failure) ->
                    calls.add(name + (failure == null ? ":after" : ":fault")));
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

    public static class ForwardedMessage {
    }

    public static class AsyncForwardingConsumer implements Consumer<TestMessage> {
        private final SendEndpointProvider sendEndpointProvider;

        @Inject
        public AsyncForwardingConsumer(SendEndpointProvider sendEndpointProvider) {
            this.sendEndpointProvider = sendEndpointProvider;
        }

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            return CompletableFuture.runAsync(() -> sendEndpointProvider
                    .getSendEndpoint("loopback://forwarded")
                    .send(new ForwardedMessage(), CancellationToken.none)
                    .join());
        }
    }

    public static class ForwardedMessageConsumer implements Consumer<ForwardedMessage> {
        static CompletableFuture<ForwardedMessage> received = new CompletableFuture<>();

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<ForwardedMessage> context) {
            received.complete(context.getMessage());
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
    public void consumeFiltersWrapRetryAndOnlyDownstreamFiltersAreReentered() {
        RetryingConsumer.attempts = 0;
        RetryingConsumer.calls = new ArrayList<>();
        ServiceCollection services = ServiceCollection.create();
        MediatorBus bus = MediatorBus.configure(services, cfg ->
                cfg.addConsumer(RetryingConsumer.class, TestMessage.class, pipe -> {
                    pipe.useFilter(new RecordingConsumeFilter("outer", RetryingConsumer.calls));
                    pipe.useMessageRetry(retry -> retry.immediate(1));
                    pipe.useFilter(new RecordingConsumeFilter("inner", RetryingConsumer.calls));
                }));

        bus.publish(new TestMessage("retry"));

        Assertions.assertEquals(2, RetryingConsumer.attempts);
        Assertions.assertEquals(
                List.of(
                        "outer:before",
                        "inner:before",
                        "consumer:1",
                        "inner:fault",
                        "inner:before",
                        "consumer:2",
                        "inner:after",
                        "outer:after"),
                RetryingConsumer.calls);
    }

    @Test
    public void retryExhaustionPropagatesTerminalFailureThroughUpstreamFiltersOnce() {
        FailingConsumer.attempts = 0;
        FailingConsumer.calls = new ArrayList<>();
        ServiceCollection services = ServiceCollection.create();
        MediatorBus bus = MediatorBus.configure(services, cfg ->
                cfg.addConsumer(FailingConsumer.class, TestMessage.class, pipe -> {
                    pipe.useFilter(new RecordingConsumeFilter("outer", FailingConsumer.calls));
                    pipe.useMessageRetry(retry -> retry.immediate(1));
                    pipe.useFilter(new RecordingConsumeFilter("inner", FailingConsumer.calls));
                }));

        CompletionException exception = Assertions.assertThrows(
                CompletionException.class,
                () -> bus.publish(new TestMessage("fail")));

        Assertions.assertInstanceOf(IllegalStateException.class, exception.getCause());
        Assertions.assertEquals("exhausted", exception.getCause().getMessage());
        Assertions.assertEquals(2, FailingConsumer.attempts);
        Assertions.assertEquals(
                List.of(
                        "outer:before",
                        "inner:before",
                        "consumer:1",
                        "inner:fault",
                        "inner:before",
                        "consumer:2",
                        "inner:fault",
                        "outer:fault"),
                FailingConsumer.calls);
    }

    @Test
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
    public void scopedSendEndpointProviderRetainsConsumeContextAcrossAsyncDispatch() {
        ServiceCollection services = ServiceCollection.create();
        MediatorBus bus = MediatorBus.configure(services, cfg -> {
            cfg.addConsumer(AsyncForwardingConsumer.class);
            cfg.addConsumer(ForwardedMessageConsumer.class);
        });

        ForwardedMessageConsumer.received = new CompletableFuture<>();
        bus.publish(new TestMessage("async"));

        Assertions.assertNotNull(ForwardedMessageConsumer.received.join());
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
