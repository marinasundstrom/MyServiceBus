package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;

import java.net.URI;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.serialization.MessageSerializationContext;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;

class UnknownMessageTypeTest {
    static class StubTransportFactory implements TransportFactory {
        Function<TransportMessage, CompletableFuture<Void>> handler;

        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> { };
        }

        @Override
        public ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
                Function<TransportMessage, CompletableFuture<Void>> handler) {
            this.handler = handler;
            return new ReceiveTransport() {
                @Override public void start() { }
                @Override public void stop() { }
            };
        }

        @Override public String getPublishAddress(String exchange) { return exchange; }
        @Override public String getSendAddress(String queue) { return queue; }
    }

    @Test
    void skipsUnregisteredMessageType() throws Exception {
        StubTransportFactory transportFactory = new StubTransportFactory();
        ServiceCollection services = new ServiceCollection();
        services.addSingleton(TransportFactory.class, sp -> () -> transportFactory);
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> uri -> new SendEndpoint() {
            @Override public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                return CompletableFuture.completedFuture(null);
            }
        });
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(ctx -> CompletableFuture.completedFuture(null)));
        ServiceProvider provider = services.buildServiceProvider();

        MessageBusImpl bus = new MessageBusImpl(provider);
        class SampleMessage { }
        bus.addHandler("queue", SampleMessage.class, "exchange", ctx -> CompletableFuture.completedFuture(null), null, null, null);

        MessageSerializationContext<Object> ctx = new MessageSerializationContext<>(Map.of("value", 1));
        ctx.setMessageId(UUID.randomUUID());
        ctx.setMessageType(List.of("urn:message:Unknown"));
        ctx.setHeaders(new java.util.HashMap<>());
        byte[] body = new EnvelopeMessageSerializer().serialize(ctx);
        TransportMessage tm = new TransportMessage(body, new java.util.HashMap<>());

        assertDoesNotThrow(() -> transportFactory.handler.apply(tm).join());
    }
}
