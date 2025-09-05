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
        MessageDeserializer md;
        try {
            md = serviceProvider.getService(MessageDeserializer.class);
        } catch (Exception ex) {
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
                if (faultAddress == null) {
                    faultAddress = transportFactory.getPublishAddress(consumerDef.getQueueName() + "_error");
                }

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

    public <T> void addHandler(String queueName, Class<T> messageType, String exchange,
            java.util.function.Function<ConsumeContext<T>, CompletableFuture<Void>> handler,
            Integer retryCount, java.time.Duration retryDelay) throws Exception {
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
                if (faultAddress == null) {
                    faultAddress = transportFactory.getPublishAddress(queueName + "_error");
                }
                ConsumeContext<T> ctx = new ConsumeContext<>((T) envelope.getMessage(), envelope.getHeaders(),
                        envelope.getResponseAddress(), faultAddress, CancellationToken.none, transportSendEndpointProvider);
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
