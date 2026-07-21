package com.myservicebus;

import java.net.URI;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.HashSet;
import java.util.concurrent.CompletableFuture;
import java.time.Duration;
import java.time.Instant;
import java.util.concurrent.TimeUnit;
import java.util.function.Function;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.serialization.InboundMessage;
import com.myservicebus.serialization.InboundMessageResolver;
import com.myservicebus.serialization.MessageEnvelopeMode;
import com.myservicebus.serialization.MessageDeserializer;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.BusTopology;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.EntityNameFormatter;
import com.myservicebus.MessageUrn;
import com.myservicebus.Consumer;

public class MessageBusImpl implements MessageBus, ReceiveEndpointConnector {
    private final ServiceProvider serviceProvider;
    private final TransportFactory transportFactory;
    private final TransportSendEndpointProvider transportSendEndpointProvider;
    private final PublishPipe publishPipe;
    private final PublishContextFactory publishContextFactory;
    private final Logger logger;
    private final InboundMessageResolver inboundMessageResolver;
    private final List<ReceiveTransport> receiveTransports = new ArrayList<>();
    private final URI address;
    private final BusTopology topology;
    private final Set<String> consumerRegistrations = new HashSet<>();
    private final Set<String> messageTypes = new HashSet<>();
    private final Function<Class<?>, ConsumerFactory> consumerFactoryFactory;
    private volatile BusState state = BusState.STOPPED;

    public MessageBusImpl(ServiceProvider serviceProvider) {
        this(serviceProvider, type -> new ScopeConsumerFactory(serviceProvider));
    }

    public MessageBusImpl(ServiceProvider serviceProvider, Function<Class<?>, ConsumerFactory> consumerFactoryFactory) {
        this.serviceProvider = serviceProvider;
        this.consumerFactoryFactory = consumerFactoryFactory;
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
        InboundMessageResolver resolver = serviceProvider.getService(InboundMessageResolver.class);
        if (resolver == null) {
            resolver = new com.myservicebus.serialization.DefaultInboundMessageResolver(md);
        }
        this.inboundMessageResolver = resolver;
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

    public synchronized void start() throws Exception {
        if (state == BusState.STARTED) {
            return;
        }

        state = BusState.STARTING;
        List<ReceiveTransport> startedTransports = new ArrayList<>();
        try {
            TransportCapabilityRequirements requirements = serviceProvider.getService(TransportCapabilityRequirements.class);
            TransportCapabilityDescriptor descriptor = serviceProvider.getService(TransportCapabilityDescriptor.class);
            if (requirements != null && !requirements.items().isEmpty()) {
                if (descriptor == null && transportFactory != null) {
                    descriptor = transportFactory.getCapabilities();
                }
                if (descriptor == null) {
                    descriptor = TransportCapabilityDescriptors.unknown("unregistered");
                }
                TransportCapabilityValidator.validate(descriptor, requirements.items());
            }

            TopologyRegistry topology = serviceProvider.getService(TopologyRegistry.class);

            for (ConsumerTopology consumerDef : topology.getConsumers()) {
                addConsumer(consumerDef);
            }

            for (ReceiveTransport transport : receiveTransports) {
                transport.start();
                startedTransports.add(transport);
            }

            state = BusState.STARTED;
            if (logger != null) {
                logger.info("Service bus started");
            }
        } catch (Exception startupFailure) {
            for (int i = startedTransports.size() - 1; i >= 0; i--) {
                try {
                    startedTransports.get(i).stop();
                } catch (Exception ignored) {
                    // Preserve the startup failure while making a best effort to roll back.
                }
            }
            state = BusState.STOPPED;
            throw startupFailure;
        }
    }

    public void addConsumer(ConsumerTopology consumerDef) throws Exception {
        String messageUrn = MessageUrn.forClass(consumerDef.getBindings().get(0).getMessageType());
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
        ConsumerFactory factory = consumerFactoryFactory.apply(consumerDef.getConsumerType());
        @SuppressWarnings({ "unchecked", "rawtypes" })
        Filter<ConsumeContext<Object>> consumerFilter = new ConsumerMessageFilter(consumerDef.getConsumerType(), factory);
        configurator.useFilter(consumerFilter);
        Pipe<ConsumeContext<Object>> pipe = configurator.build(serviceProvider);

        MessageSerializer endpointSerializer = consumerDef.getSerializerClass() != null
                ? consumerDef.getSerializerClass().getDeclaredConstructor().newInstance()
                : null;
        boolean rawSerializer = isRawSerializer(endpointSerializer);
        TransportSendEndpointProvider provider = endpointSerializer != null
                ? transportSendEndpointProvider.withSerializer(endpointSerializer)
                : transportSendEndpointProvider;

        Function<TransportMessage, CompletableFuture<Void>> handler = transportMessage -> {
            try {
                InboundMessage inboundMessage = inboundMessageResolver.resolve(transportMessage);
                String messageTypeUrn = inboundMessage.getMessageType();
                MessageBinding binding;

                if (messageTypeUrn == null && rawSerializer) {
                    binding = consumerDef.getBindings().get(0);
                } else {
                    final String resolvedMessageTypeUrn = messageTypeUrn;
                    binding = consumerDef.getBindings().stream()
                            .filter(b -> MessageUrn.forClass(b.getMessageType()).equals(resolvedMessageTypeUrn))
                            .findFirst()
                            .orElse(null);
                    if (binding == null) {
                        if (logger != null) {
                            logger.warn("Received message with unregistered type {}", messageTypeUrn);
                        }
                        return CompletableFuture.completedFuture(null);
                    }
                }

                Type messageType = resolveMessageType(consumerDef.getConsumerType(), binding.getMessageType());
                Object message = inboundMessage.getMessage(messageType);
                Map<String, Object> headers = inboundMessage.getHeaders();
                String responseAddress = inboundMessage.getResponseAddress();
                String faultAddress = inboundMessage.getFaultAddress();
                String errorAddress = transportFactory.getErrorAddress(consumerDef.getQueueName());

                ConsumeContext<Object> ctx = new ConsumeContext<>(
                        message,
                        headers,
                        responseAddress,
                        faultAddress,
                        errorAddress,
                        CancellationToken.none(),
                        provider,
                        this.address,
                        this::getPublishAddress,
                        inboundMessage.getRequestId(),
                        inboundMessage.getCorrelationId(),
                        inboundMessage.getConversationId(),
                        inboundMessage.getInitiatorId());
                if (logger != null) {
                    logger.debug("Received {}", messageTypeUrn);
                }
                return pipe.send(ctx);
            } catch (Exception e) {
                CompletableFuture<Void> f = new CompletableFuture<>();
                f.completeExceptionally(e);
                return f;
            }
        };

        java.util.function.Function<String, Boolean> isRegistered = urn -> urn == null ? rawSerializer : messageTypes.contains(urn);
        ReceiveEndpointTransportTopology endpointTopology = new ReceiveEndpointTransportTopology(
                consumerDef.getQueueName(),
                true,
                false,
                consumerDef.getPrefetchCount() != null ? consumerDef.getPrefetchCount() : 0,
                consumerDef.getBindings(),
                consumerDef.getQueueArguments());
        ReceiveTransport transport = transportFactory.createReceiveTransport(endpointTopology, handler, isRegistered);
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

        boolean rawSerializer = isRawSerializer(serializer);
        TransportSendEndpointProvider provider = serializer != null
                ? transportSendEndpointProvider.withSerializer(serializer)
                : transportSendEndpointProvider;

        java.util.function.Function<TransportMessage, CompletableFuture<Void>> transportHandler = tm -> {
            try {
                String expectedUrn = MessageUrn.forClass(messageType);
                InboundMessage inboundMessage = inboundMessageResolver.resolve(tm);
                String messageTypeUrn = inboundMessage.getMessageType();
                if (messageTypeUrn != null && !expectedUrn.equals(messageTypeUrn)) {
                    if (logger != null) {
                        logger.warn("Received message with unregistered type {}", messageTypeUrn);
                    }
                    return CompletableFuture.completedFuture(null);
                }

                if (messageTypeUrn == null && !rawSerializer) {
                    if (logger != null) {
                        logger.warn("Received message with unregistered type {}", messageTypeUrn);
                    }
                    return CompletableFuture.completedFuture(null);
                }

                T typedMessage = inboundMessage.getMessage(messageType);
                Map<String, Object> headers = inboundMessage.getHeaders();
                String responseAddress = inboundMessage.getResponseAddress();
                String faultAddress = inboundMessage.getFaultAddress();
                String errorAddress = transportFactory.getErrorAddress(queueName);
                ConsumeContext<T> ctx = new ConsumeContext<>(typedMessage, headers,
                        responseAddress, faultAddress, errorAddress, CancellationToken.none(),
                        provider,
                        this.address,
                        this::getPublishAddress,
                        inboundMessage.getRequestId(),
                        inboundMessage.getCorrelationId(),
                        inboundMessage.getConversationId(),
                        inboundMessage.getInitiatorId());
                if (logger != null) {
                    logger.debug("Received {}", messageTypeUrn);
                }
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

        String expectedUrn = MessageUrn.forClass(messageType);
        java.util.function.Function<String, Boolean> isRegisteredHandler = urn -> expectedUrn.equals(urn) || (rawSerializer && urn == null);

        ReceiveEndpointTransportTopology endpointTopology = new ReceiveEndpointTransportTopology(
                queueName,
                true,
                false,
                prefetchCount != null ? prefetchCount : 0,
                bindings,
                queueArguments);
        ReceiveTransport transport = transportFactory.createReceiveTransport(
                endpointTopology, transportHandler, isRegisteredHandler);
        receiveTransports.add(transport);
    }

    private String getPublishAddress(String entityName) {
        return transportFactory != null
                ? transportFactory.getPublishAddress(entityName)
                : "exchange:" + entityName;
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

    private static boolean isRawSerializer(MessageSerializer serializer) {
        return serializer != null && serializer.getEnvelopeMode() == MessageEnvelopeMode.RAW;
    }

    public synchronized void stop() throws Exception {
        if (state == BusState.STOPPED) {
            return;
        }

        state = BusState.STOPPING;
        try {
            for (int i = receiveTransports.size() - 1; i >= 0; i--) {
                receiveTransports.get(i).stop();
            }
        } finally {
            state = BusState.STOPPED;
        }

        if (logger != null) {
            logger.info("Service bus stopped");
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
        if (state != BusState.STARTED)
            return notStartedFuture();
        if (message == null)
            return CompletableFuture.failedFuture(new IllegalArgumentException("Message cannot be null"));
        PublishContext ctx = publishContextFactory.create(message, cancellationToken);
        return publish(ctx);
    }

    @Override
    public <T> CompletableFuture<Void> publish(T message, java.util.function.Consumer<PublishContext> contextCallback,
            CancellationToken cancellationToken) {
        if (state != BusState.STARTED)
            return notStartedFuture();
        PublishContext ctx = publishContextFactory.create(message, cancellationToken);
        contextCallback.accept(ctx);
        return publish(ctx);
    }

    @Override
    public CompletableFuture<Void> publish(PublishContext context) {
        if (state != BusState.STARTED)
            return notStartedFuture();
        String exchange = EntityNameFormatter.format(context.getMessage().getClass());
        String address = transportFactory.getPublishAddress(exchange);
        context.setSourceAddress(this.address);
        context.setDestinationAddress(URI.create(address));

        if (logger != null) {
            logger.debug("Publishing {} to {}", context.getMessage().getClass().getSimpleName(), context.getDestinationAddress());
        }

        CompletableFuture<Void> delayFuture;
        Instant scheduled = context.getScheduledEnqueueTime();
        if (scheduled != null) {
            Duration delay = Duration.between(Instant.now(), scheduled);
            if (delay.isNegative()) {
                delay = Duration.ZERO;
            }
            delayFuture = new CompletableFuture<>();
            CompletableFuture.delayedExecutor(delay.toMillis(), TimeUnit.MILLISECONDS)
                    .execute(() -> delayFuture.complete(null));
        } else {
            delayFuture = CompletableFuture.completedFuture(null);
        }

        return delayFuture.thenCompose(v -> publishPipe.send(context).thenCompose(x -> {
            SendEndpointProvider provider = serviceProvider.getService(SendEndpointProvider.class);
            SendEndpoint endpoint = provider.getSendEndpoint(address);
            return endpoint.send(context);
        }));
    }

    public <T> CompletableFuture<Void> publish(T message) {
        return publish(message, CancellationToken.none());
    }

    @Override
    public PublishEndpoint getPublishEndpoint() {
        return this;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        SendEndpointProvider provider = serviceProvider.getService(SendEndpointProvider.class);
        SendEndpoint endpoint = provider.getSendEndpoint(uri);
        return new SendEndpoint() {
            @Override
            public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                return state == BusState.STARTED
                        ? endpoint.send(message, cancellationToken)
                        : notStartedFuture();
            }
        };
    }

    boolean isStarted() {
        return state == BusState.STARTED;
    }

    static <T> CompletableFuture<T> notStartedFuture() {
        return CompletableFuture.failedFuture(new IllegalStateException("The service bus is not started."));
    }

    private enum BusState {
        STOPPED,
        STARTING,
        STARTED,
        STOPPING
    }
}
