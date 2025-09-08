package com.myservicebus;

import java.net.URI;
import java.util.ArrayList;
import java.util.List;
import java.util.Set;
import java.util.HashSet;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.serialization.MessageDeserializer;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.BusTopology;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.NamingConventions;
import com.myservicebus.Consumer;

public class MessageBusImpl implements MessageBus, ReceiveEndpointConnector {
    private final ServiceProvider serviceProvider;
    private final TransportFactory transportFactory;
    private final TransportSendEndpointProvider transportSendEndpointProvider;
    private final PublishPipe publishPipe;
    private final PublishContextFactory publishContextFactory;
    private final Logger logger;
    private final MessageDeserializer messageDeserializer;
    private final List<ReceiveTransport> receiveTransports = new ArrayList<>();
    private final URI address;
    private final BusTopology topology;
    private final Set<String> consumerRegistrations = new HashSet<>();
    private final Set<String> messageTypes = new HashSet<>();

    public MessageBusImpl(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
        this.transportFactory = serviceProvider.getService(TransportFactory.class);
        this.transportSendEndpointProvider = serviceProvider.getService(TransportSendEndpointProvider.class);
        PublishContextFactory factory = serviceProvider.getService(PublishContextFactory.class);
        this.publishContextFactory = factory != null ? factory : new DefaultPublishContextFactory();
        this.publishPipe = serviceProvider.getService(PublishPipe.class);
        LoggerFactory loggerFactory = serviceProvider.getService(LoggerFactory.class);
        this.logger = loggerFactory != null ? loggerFactory.create(MessageBusImpl.class) : null;
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
        String messageUrn = NamingConventions.getMessageUrn(consumerDef.getBindings().get(0).getMessageType());
        String key = consumerDef.getQueueName() + "|" + messageUrn;
        if (consumerRegistrations.contains(key)) {
            if (logger != null) {
                logger.debug("Consumer for '{}' on '{}' already registered, skipping", messageUrn,
                        consumerDef.getQueueName());
            }
            return;
        }
        messageTypes.add(messageUrn);

        PipeConfigurator<ConsumeContext<Object>> configurator = new PipeConfigurator<>();
        configurator.useFilter(new OpenTelemetryConsumeFilter<>());
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<Object>> errorFilter = new ErrorTransportFilter(serviceProvider);
        configurator.useFilter(errorFilter);
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<Object>> faultFilter = new ConsumerFaultFilter(serviceProvider,
                consumerDef.getConsumerType());
        configurator.useFilter(faultFilter);
        if (consumerDef.getConfigure() != null)
            consumerDef.getConfigure().accept(configurator);
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<Object>> consumerFilter = new ConsumerMessageFilter(serviceProvider,
                consumerDef.getConsumerType());
        configurator.useFilter(consumerFilter);
        Pipe<ConsumeContext<Object>> pipe = configurator.build(serviceProvider);

        MessageSerializer endpointSerializer = consumerDef.getSerializerClass() != null
                ? consumerDef.getSerializerClass().getDeclaredConstructor().newInstance()
                : null;
        TransportSendEndpointProvider provider = endpointSerializer != null
                ? transportSendEndpointProvider.withSerializer(endpointSerializer)
                : transportSendEndpointProvider;

        Function<TransportMessage, CompletableFuture<Void>> handler = transportMessage -> {
            try {
                Envelope<Object> envelope = messageDeserializer.deserialize(transportMessage.getBody(), Object.class);
                String messageTypeUrn = envelope.getMessageType() != null && !envelope.getMessageType().isEmpty()
                        ? envelope.getMessageType().get(0)
                        : null;
                MessageBinding binding = consumerDef.getBindings().stream()
                        .filter(b -> NamingConventions.getMessageUrn(b.getMessageType()).equals(messageTypeUrn))
                        .findFirst()
                        .orElse(null);
                if (binding == null) {
                    logger.warn("Received message with unregistered type {}", messageTypeUrn);
                    return CompletableFuture.completedFuture(null);
                }

                Type messageType = resolveMessageType(consumerDef.getConsumerType(), binding.getMessageType());
                Envelope<?> typedEnvelope = messageDeserializer.deserialize(transportMessage.getBody(),
                        messageType);

                String faultAddress = typedEnvelope.getFaultAddress();
                if (faultAddress == null && transportMessage.getHeaders() != null) {
                    Object header = transportMessage.getHeaders().get(MessageHeaders.FAULT_ADDRESS);
                    if (header instanceof String s) {
                        faultAddress = s;
                    }
                }
                String errorAddress = transportFactory.getPublishAddress(consumerDef.getQueueName() + "_error");

                ConsumeContext<Object> ctx = new ConsumeContext<>(
                        typedEnvelope.getMessage(),
                        typedEnvelope.getHeaders(),
                        typedEnvelope.getResponseAddress(),
                        faultAddress,
                        errorAddress,
                        CancellationToken.none,
                        provider,
                        this.address);
                return pipe.send(ctx);
            } catch (Exception e) {
                CompletableFuture<Void> f = new CompletableFuture<>();
                f.completeExceptionally(e);
                return f;
            }
        };

        java.util.function.Function<String, Boolean> isRegistered = urn -> messageTypes.contains(urn);
        ReceiveTransport transport = transportFactory.createReceiveTransport(consumerDef.getQueueName(),
                consumerDef.getBindings(), handler, isRegistered,
                consumerDef.getPrefetchCount() != null ? consumerDef.getPrefetchCount() : 0,
                consumerDef.getQueueArguments());
        receiveTransports.add(transport);
        consumerRegistrations.add(key);
    }

    public <T> void addHandler(String queueName, Class<T> messageType, String exchange,
            java.util.function.Function<ConsumeContext<T>, CompletableFuture<Void>> handler,
            Integer retryCount, java.time.Duration retryDelay, Integer prefetchCount,
            java.util.Map<String, Object> queueArguments, MessageSerializer serializer) throws Exception {
        PipeConfigurator<ConsumeContext<T>> configurator = new PipeConfigurator<>();
        configurator.useFilter(new OpenTelemetryConsumeFilter<>());
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<T>> errorFilter = new ErrorTransportFilter(serviceProvider);
        configurator.useFilter(errorFilter);
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<T>> faultFilter = new HandlerFaultFilter(serviceProvider);
        configurator.useFilter(faultFilter);
        if (retryCount != null) {
            configurator.useRetry(retryCount, retryDelay);
        }
        configurator.useFilter(new HandlerMessageFilter<>(handler));
        Pipe<ConsumeContext<T>> pipe = configurator.build(serviceProvider);

        TransportSendEndpointProvider provider = serializer != null
                ? transportSendEndpointProvider.withSerializer(serializer)
                : transportSendEndpointProvider;

        java.util.function.Function<TransportMessage, CompletableFuture<Void>> transportHandler = tm -> {
            try {
                Envelope<Object> envelope = messageDeserializer.deserialize(tm.getBody(), Object.class);
                String messageTypeUrn = envelope.getMessageType() != null && !envelope.getMessageType().isEmpty()
                        ? envelope.getMessageType().get(0)
                        : null;
                String expectedUrn = NamingConventions.getMessageUrn(messageType);
                if (!expectedUrn.equals(messageTypeUrn)) {
                    logger.warn("Received message with unregistered type {}", messageTypeUrn);
                    return CompletableFuture.completedFuture(null);
                }

                Envelope<?> typedEnvelope = messageDeserializer.deserialize(tm.getBody(), messageType);
                String faultAddress = typedEnvelope.getFaultAddress();
                if (faultAddress == null && tm.getHeaders() != null) {
                    Object header = tm.getHeaders().get(MessageHeaders.FAULT_ADDRESS);
                    if (header instanceof String s) {
                        faultAddress = s;
                    }
                }
                String errorAddress = transportFactory.getPublishAddress(queueName + "_error");
                ConsumeContext<T> ctx = new ConsumeContext<>((T) typedEnvelope.getMessage(), typedEnvelope.getHeaders(),
                        typedEnvelope.getResponseAddress(), faultAddress, errorAddress, CancellationToken.none,
                        provider,
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

        String expectedUrn = NamingConventions.getMessageUrn(messageType);
        java.util.function.Function<String, Boolean> isRegisteredHandler = urn -> expectedUrn.equals(urn);

        ReceiveTransport transport = transportFactory.createReceiveTransport(queueName, bindings, transportHandler,
                isRegisteredHandler, prefetchCount != null ? prefetchCount : 0, queueArguments);
        receiveTransports.add(transport);
    }

    private static Type resolveMessageType(Class<?> consumerType, Class<?> bindingType) {
        for (Type iface : consumerType.getGenericInterfaces()) {
            if (iface instanceof ParameterizedType pt) {
                Type raw = pt.getRawType();
                if (raw instanceof Class<?> rawClass && Consumer.class.isAssignableFrom(rawClass)) {
                    Type actual = pt.getActualTypeArguments()[0];
                    if (actual instanceof ParameterizedType p) {
                        if (p.getRawType().equals(bindingType)) {
                            return p;
                        }
                    } else if (actual.equals(bindingType)) {
                        return actual;
                    }
                }
            }
        }
        return bindingType;
    }

    public void stop() throws Exception {
        for (ReceiveTransport transport : receiveTransports) {
            transport.stop();
        }
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
        PublishContext ctx = publishContextFactory.create(message, cancellationToken);
        return publish(ctx);
    }

    @Override
    public <T> CompletableFuture<Void> publish(T message, java.util.function.Consumer<PublishContext> contextCallback,
            CancellationToken cancellationToken) {
        PublishContext ctx = publishContextFactory.create(message, cancellationToken);
        contextCallback.accept(ctx);
        return publish(ctx);
    }

    @Override
    public CompletableFuture<Void> publish(PublishContext context) {
        String exchange = NamingConventions.getExchangeName(context.getMessage().getClass());
        String address = transportFactory.getPublishAddress(exchange);
        context.setSourceAddress(this.address);
        context.setDestinationAddress(URI.create(address));
        return publishPipe.send(context).thenCompose(v -> {
            SendEndpointProvider provider = serviceProvider.getService(SendEndpointProvider.class);
            SendEndpoint endpoint = provider.getSendEndpoint(address);
            return endpoint.send(context).thenRun(() -> {
                if (logger != null) {
                    logger.debug("Published message of type {}", context.getMessage().getClass().getSimpleName());
                }
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
