package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.net.URI;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.rabbitmq.ConnectionProvider;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.rabbitmq.client.ConnectionFactory;

class PublishContextAddressTest {
    static class TestMessage {
    }

    @Test
    void publish_sets_source_and_destination_addresses() throws Exception {
        AtomicReference<SendContext> captured = new AtomicReference<>();

        ServiceCollection services = new ServiceCollection();
        services.addSingleton(SendPipe.class, sp -> () -> new SendPipe(new PipeConfigurator<SendContext>().build()));
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(new PipeConfigurator<SendContext>().build()));
        services.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> uri -> new SendEndpoint() {
            @Override
            public CompletableFuture<Void> send(SendContext ctx) {
                captured.set(ctx);
                return sp.getService(SendPipe.class).send(ctx);
            }

            @Override
            public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                return send(new SendContext(message, cancellationToken));
            }
        });
        services.addSingleton(SendEndpointProvider.class, sp -> () -> new SendEndpointProviderImpl(
                sp.getService(ConsumeContextProvider.class), sp.getService(TransportSendEndpointProvider.class)));
        services.addSingleton(ConnectionProvider.class, sp -> () -> new ConnectionProvider(new ConnectionFactory()));
        services.addSingleton(TransportFactory.class, sp -> () -> new TransportFactory() {
            @Override
            public SendTransport getSendTransport(URI address) {
                return data -> {
                };
            }

            @Override
            public ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
                    Function<TransportMessage, CompletableFuture<Void>> handler) {
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

        bus.publish(new TestMessage());

        assertEquals(URI.create("rabbitmq://localhost/"), captured.get().getSourceAddress());
        assertEquals(URI.create("rabbitmq://localhost/exchange/TestApp:TestMessage"),
                captured.get().getDestinationAddress());
    }
}
