package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.net.URI;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.serialization.MessageSerializationContext;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.topology.MessageBinding;

class ReceiveEndpointSerializerTest {
    public static class InputMessage { public String value = "hi"; }
    public static class OutputMessage { }

    static class CustomSerializer implements MessageSerializer {
        @Override
        public <T> byte[] serialize(MessageSerializationContext<T> context) {
            context.getHeaders().put("content_type", "application/custom");
            return new byte[0];
        }
    }

    static class StubProvider implements TransportSendEndpointProvider {
        MessageSerializer serializer;
        String contentType;
        @Override
        public TransportSendEndpointProvider withSerializer(MessageSerializer serializer) {
            this.serializer = serializer;
            return this;
        }
        @Override
        public SendEndpoint getSendEndpoint(String uri) {
            return new SendEndpoint() {
                @Override
                public CompletableFuture<Void> send(SendContext ctx) {
                    try {
                        ctx.serialize(serializer);
                    } catch (Exception ignored) {}
                    contentType = (String) ctx.getHeaders().get("content_type");
                    return CompletableFuture.completedFuture(null);
                }
                @Override
                public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                    return send(new SendContext(message, cancellationToken));
                }
            };
        }
    }

    static class StubTransportFactory implements TransportFactory {
        java.util.function.Function<TransportMessage, CompletableFuture<Void>> handler;
        @Override
        public SendTransport getSendTransport(URI address) { return (data, headers, contentType) -> {}; }
        @Override
        public ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
                java.util.function.Function<TransportMessage, CompletableFuture<Void>> h,
                java.util.function.Function<String, Boolean> isRegistered, int prefetchCount,
                Map<String, Object> queueArguments) {
            handler = h;
            return new ReceiveTransport() {
                public void start() {}
                public void stop() {}
            };
        }
        @Override public String getPublishAddress(String exchange) { return "loopback://" + exchange; }
        @Override public String getSendAddress(String queue) { return "loopback://" + queue; }
    }

    @Test
    void handler_uses_custom_serializer() throws Exception {
        ServiceCollection services = new ServiceCollection();
        StubTransportFactory factory = new StubTransportFactory();
        StubProvider provider = new StubProvider();
        services.addSingleton(TransportFactory.class, sp -> () -> factory);
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> provider);
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(ctx -> CompletableFuture.completedFuture(null)));
        services.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        services.addSingleton(SendEndpointProvider.class, sp -> () -> new SendEndpointProviderImpl(sp.getService(ConsumeContextProvider.class), sp.getService(TransportSendEndpointProvider.class)));
        ServiceProvider sp = services.buildServiceProvider();
        MessageBusImpl bus = new MessageBusImpl(sp);

        bus.addHandler("input", InputMessage.class, "input", ctx -> {
            return ctx.publish(new OutputMessage());
        }, null, null, null, null, new CustomSerializer());

        MessageSerializationContext<Object> mctx = new MessageSerializationContext<>(new InputMessage());
        mctx.setMessageId(java.util.UUID.randomUUID());
        mctx.setMessageType(List.of(MessageUrn.forClass(InputMessage.class)));
        mctx.setHeaders(new HashMap<>());
        byte[] body = new EnvelopeMessageSerializer().serialize(mctx);
        factory.handler.apply(new TransportMessage(body, new HashMap<>())).join();

        assertEquals("application/custom", provider.contentType);
    }
}
