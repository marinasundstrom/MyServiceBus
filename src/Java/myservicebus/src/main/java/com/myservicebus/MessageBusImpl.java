package com.myservicebus;

import java.net.URI;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.slf4j.Logger;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.MessageDeserializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.BusTopology;
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
    private final URI address;
    private final BusTopology topology;

    public MessageBusImpl(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
        this.transportFactory = serviceProvider.getService(TransportFactory.class);
        this.transportSendEndpointProvider = serviceProvider.getService(TransportSendEndpointProvider.class);
        this.publishPipe = serviceProvider.getService(PublishPipe.class);
        this.logger = serviceProvider.getService(Logger.class);
        MessageDeserializer md = serviceProvider.getService(MessageDeserializer.class);
        if (md == null) {
            md = new com.myservicebus.serialization.EnvelopeMessageDeserializer();
        }
        this.messageDeserializer = md;
        BusTopology top = serviceProvider.getService(TopologyRegistry.class);
        this.topology = top != null ? top : new TopologyRegistry();
        URI configuredAddress = serviceProvider.getService(URI.class);
        this.address = configuredAddress != null ? configuredAddress : URI.create("loopback://localhost/");
    }

    public static MessageBus configure(ServiceCollection services,
            java.util.function.Consumer<BusRegistrationConfigurator> configure) {
        var cfg = new BusRegistrationConfiguratorImpl(services);
        configure.accept(cfg);
        cfg.complete();
        return new MessageBusImpl(services.buildServiceProvider());
    }

    public void start() {
        try {
            TopologyRegistry topology = serviceProvider.getService(TopologyRegistry.class);

            for (ConsumerTopology consumerDef : topology.getConsumers()) {
                addConsumer(consumerDef);
            }

            for (ReceiveTransport transport : receiveTransports) {
                transport.start();
            }
        } catch (RuntimeException e) {
            throw new IllegalStateException("Failed to start message bus", e);
        }
    }

    public void addConsumer(ConsumerTopology consumerDef) {
        if (consumerDef == null)
            throw new IllegalArgumentException("Consumer topology cannot be null");
        if (consumerDef.getQueueName() == null || consumerDef.getQueueName().isBlank())
            throw new IllegalArgumentException("Queue name cannot be null or empty");
        if (consumerDef.getBindings() == null || consumerDef.getBindings().isEmpty())
            throw new IllegalStateException("Consumer must have at least one binding");
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

        Function<TransportMessage, CompletableFuture<Void>> handler = transportMessage -> {
            try {
                MessageBinding binding = consumerDef.getBindings().get(0);
                Envelope<?> envelope = messageDeserializer.deserialize(transportMessage.getBody(),
                        binding.getMessageType());

                String faultAddress = envelope.getFaultAddress();
                if (faultAddress == null && transportMessage.getHeaders() != null) {
                    Object header = transportMessage.getHeaders().get(MessageHeaders.FAULT_ADDRESS);
                    if (header instanceof String s) {
                        faultAddress = s;
                    }
                }
                String errorAddress = transportFactory.getPublishAddress(consumerDef.getQueueName() + "_error");

                ConsumeContext<Object> ctx = new ConsumeContext<>(
                        envelope.getMessage(),
                        envelope.getHeaders(),
                        envelope.getResponseAddress(),
                        faultAddress,
                        errorAddress,
                        CancellationToken.none,
                        transportSendEndpointProvider,
                        this.address);
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

    public <T> void addHandler(String queueName, Class<T> messageType, String exchange,
            java.util.function.Function<ConsumeContext<T>, CompletableFuture<Void>> handler,
            Integer retryCount, java.time.Duration retryDelay) {
        if (queueName == null || queueName.isBlank())
            throw new IllegalArgumentException("Queue name cannot be null or empty");
        if (messageType == null)
            throw new IllegalArgumentException("Message type cannot be null");
        if (exchange == null || exchange.isBlank())
            throw new IllegalArgumentException("Exchange cannot be null or empty");
        if (handler == null)
            throw new IllegalArgumentException("Handler cannot be null");
        if (retryCount != null && retryCount < 0)
            throw new IllegalArgumentException("retryCount must be >= 0");

        PipeConfigurator<ConsumeContext<T>> configurator = new PipeConfigurator<>();
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<T>> errorFilter = new ErrorTransportFilter();
        configurator.useFilter(errorFilter);
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<T>> faultFilter = new HandlerFaultFilter(serviceProvider);
        configurator.useFilter(faultFilter);
        if (retryCount != null) {
            configurator.useRetry(retryCount, retryDelay);
        }
        configurator.useFilter(new HandlerMessageFilter<>(handler));
        Pipe<ConsumeContext<T>> pipe = configurator.build();

        java.util.function.Function<TransportMessage, CompletableFuture<Void>> transportHandler = tm -> {
            try {
                Envelope<?> envelope = messageDeserializer.deserialize(tm.getBody(), messageType);
                String faultAddress = envelope.getFaultAddress();
                if (faultAddress == null && tm.getHeaders() != null) {
                    Object header = tm.getHeaders().get(MessageHeaders.FAULT_ADDRESS);
                    if (header instanceof String s) {
                        faultAddress = s;
                    }
                }
                String errorAddress = transportFactory.getPublishAddress(queueName + "_error");
                ConsumeContext<T> ctx = new ConsumeContext<>((T) envelope.getMessage(), envelope.getHeaders(),
                        envelope.getResponseAddress(), faultAddress, errorAddress, CancellationToken.none,
                        transportSendEndpointProvider,
                        this.address);
                return pipe.send(ctx);
            } catch (Exception e) {
                CompletableFuture<Void> f = new CompletableFuture<>();
                f.completeExceptionally(e);
                return f;
            }
        };

        java.util.List<MessageBinding> bindings = new java.util.ArrayList<>();
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(messageType);
        binding.setEntityName(exchange);
        bindings.add(binding);

        ReceiveTransport transport = transportFactory.createReceiveTransport(queueName, bindings, transportHandler);
        receiveTransports.add(transport);
    }

    public void stop() {
        IllegalStateException first = null;
        for (ReceiveTransport transport : receiveTransports) {
            try {
                transport.stop();
            } catch (RuntimeException e) {
                if (first == null)
                    first = new IllegalStateException("Failed to stop message bus", e);
            }
        }
        if (first != null)
            throw first;
    }

    @Override
    public URI getAddress() {
        return address;
    }

    @Override
    public BusTopology getTopology() {
        return topology;
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
        context.setSourceAddress(this.address);
        context.setDestinationAddress(URI.create(address));
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
