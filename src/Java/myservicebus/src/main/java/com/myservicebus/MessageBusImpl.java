package com.myservicebus;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.slf4j.Logger;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.MessageDeserializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;

public class MessageBusImpl implements MessageBus, ReceiveEndpointConnector {
    private final ServiceProvider serviceProvider;
    private final TransportFactory transportFactory;
    private final TransportSendEndpointProvider transportSendEndpointProvider;
    private final PublishPipe publishPipe;
    private final Logger logger;
    private final MessageDeserializer messageDeserializer;
    private final List<ReceiveTransport> receiveTransports = new ArrayList<>();

    public MessageBusImpl(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
        this.transportFactory = serviceProvider.getService(TransportFactory.class);
        this.transportSendEndpointProvider = serviceProvider.getService(TransportSendEndpointProvider.class);
        this.publishPipe = serviceProvider.getService(PublishPipe.class);
        this.logger = serviceProvider.getService(Logger.class);
        MessageDeserializer md;
        try {
            md = serviceProvider.getService(MessageDeserializer.class);
        } catch (Exception ex) {
            md = new com.myservicebus.serialization.EnvelopeMessageDeserializer();
        }
        this.messageDeserializer = md;
    }

    public static MessageBus configure(ServiceCollection services,
            java.util.function.Consumer<BusRegistrationConfigurator> configure) {
        var cfg = new BusRegistrationConfiguratorImpl(services);
        configure.accept(cfg);
        cfg.complete();
        return new MessageBusImpl(services.buildServiceProvider());
    }

    public void start() throws Exception {
        TopologyRegistry topology = serviceProvider.getService(TopologyRegistry.class);

        for (ConsumerTopology consumerDef : topology.getConsumers()) {
            addConsumer(consumerDef);
        }

        for (ReceiveTransport transport : receiveTransports) {
            transport.start();
        }
    }

    public void addConsumer(ConsumerTopology consumerDef) throws Exception {
        PipeConfigurator<ConsumeContext<Object>> configurator = new PipeConfigurator<>();
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<Object>> errorFilter = new ErrorTransportFilter();
        configurator.useFilter(errorFilter);
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<Object>> faultFilter = new ConsumerFaultFilter(serviceProvider,
                consumerDef.getConsumerType());
        configurator.useFilter(faultFilter);
        configurator.useRetry(3);
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<Object>> consumerFilter = new ConsumerMessageFilter(serviceProvider,
                consumerDef.getConsumerType());
        configurator.useFilter(consumerFilter);
        if (consumerDef.getConfigure() != null)
            consumerDef.getConfigure().accept(configurator);
        Pipe<ConsumeContext<Object>> pipe = configurator.build();

        Function<byte[], CompletableFuture<Void>> handler = body -> {
            try {
                MessageBinding binding = consumerDef.getBindings().get(0);
                Envelope<?> envelope = messageDeserializer.deserialize(body, binding.getMessageType());

                String faultAddress = envelope.getFaultAddress() != null ? envelope.getFaultAddress()
                        : transportFactory.getPublishAddress(consumerDef.getQueueName() + "_error");

                ConsumeContext<Object> ctx = new ConsumeContext<>(
                        envelope.getMessage(),
                        envelope.getHeaders(),
                        envelope.getResponseAddress(),
                        faultAddress,
                        CancellationToken.none,
                        transportSendEndpointProvider);
                return pipe.send(ctx);
            } catch (Exception e) {
                CompletableFuture<Void> f = new CompletableFuture<>();
                f.completeExceptionally(e);
                return f;
            }
        };

        ReceiveTransport transport = transportFactory.createReceiveTransport(consumerDef.getQueueName(),
                consumerDef.getBindings(), handler);
        receiveTransports.add(transport);
    }

    public void stop() throws Exception {
        for (ReceiveTransport transport : receiveTransports) {
            transport.stop();
        }
    }

    @Override
    public <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken) {
        if (message == null)
            return CompletableFuture.failedFuture(new IllegalArgumentException("Message cannot be null"));
        SendContext ctx = new SendContext(message, cancellationToken);
        return publish(ctx);
    }

    @Override
    public CompletableFuture<Void> publish(SendContext context) {
        String exchange = NamingConventions.getExchangeName(context.getMessage().getClass());
        String address = transportFactory.getPublishAddress(exchange);
        return publishPipe.send(context).thenCompose(v -> {
            SendEndpointProvider provider = serviceProvider.getService(SendEndpointProvider.class);
            SendEndpoint endpoint = provider.getSendEndpoint(address);
            return endpoint.send(context).thenRun(() -> {
                logger.info("📤 Published message of type {}", context.getMessage().getClass().getSimpleName());
            });
        });
    }

    public <T> CompletableFuture<Void> publish(T message) {
        return publish(message, CancellationToken.none);
    }

    @Override
    public PublishEndpoint getPublishEndpoint() {
        return this;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        SendEndpointProvider provider = serviceProvider.getService(SendEndpointProvider.class);
        return provider.getSendEndpoint(uri);
    }
}
