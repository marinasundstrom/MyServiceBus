package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.net.URI;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.rabbitmq.ConnectionProvider;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.SendPipe;
import com.myservicebus.PublishPipe;
import com.myservicebus.PipeConfigurator;
import com.myservicebus.SendContext;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.TransportFactory;
import com.myservicebus.SendTransport;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.TransportMessage;
import com.myservicebus.serialization.MessageSerializer;
import com.rabbitmq.client.ConnectionFactory;

class SendEndpointAddressTest {
    static class TestMessage { }

    @Test
    void send_sets_source_and_destination_addresses() throws Exception {
        AtomicReference<SendContext> captured = new AtomicReference<>();

        ServiceCollection services = new ServiceCollection();
        services.addSingleton(SendPipe.class, sp -> () -> new SendPipe(new PipeConfigurator<SendContext>().build()));
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(new PipeConfigurator<SendContext>().build()));
        services.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> new TransportSendEndpointProvider() {
            @Override
            public SendEndpoint getSendEndpoint(String uri) {
                return new SendEndpoint() {
                    @Override
                    public CompletableFuture<Void> send(SendContext ctx) {
                        ctx.setSourceAddress(sp.getService(URI.class));
                        ctx.setDestinationAddress(URI.create(uri));
                        captured.set(ctx);
                        return sp.getService(SendPipe.class).send(ctx);
                    }

                    @Override
                    public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                        return send(new SendContext(message, cancellationToken));
                    }
                };
            }

            @Override
            public TransportSendEndpointProvider withSerializer(MessageSerializer serializer) {
                return this;
            }
        });
        services.addSingleton(SendEndpointProvider.class, sp -> () -> new SendEndpointProviderImpl(
                sp.getService(ConsumeContextProvider.class), sp.getService(TransportSendEndpointProvider.class)));
        services.addSingleton(ConnectionProvider.class, sp -> () -> new ConnectionProvider(new ConnectionFactory()));
        services.addSingleton(TransportFactory.class, sp -> () -> new TransportFactory() {
            @Override
            public SendTransport getSendTransport(URI address) {
                return (data, headers, contentType) -> {
                };
            }

            @Override
            public ReceiveTransport createReceiveTransport(String queueName, java.util.List<MessageBinding> bindings,
                    java.util.function.Function<TransportMessage, CompletableFuture<Void>> handler) {
                throw new UnsupportedOperationException();
            }

            @Override
            public String getPublishAddress(String exchange) {
                return "rabbitmq://localhost/exchange/" + exchange;
            }

            @Override
            public String getSendAddress(String queue) {
                return "rabbitmq://localhost/" + queue;
            }
        });
        services.addSingleton(URI.class, sp -> () -> URI.create("rabbitmq://localhost/"));

        MessageBus bus = new MessageBusImpl(services.buildServiceProvider());

        SendEndpoint endpoint = bus.getSendEndpoint("rabbitmq://localhost/test-queue");
        endpoint.send(new TestMessage());

        assertEquals(URI.create("rabbitmq://localhost/"), captured.get().getSourceAddress());
        assertEquals(URI.create("rabbitmq://localhost/test-queue"), captured.get().getDestinationAddress());
    }
}
