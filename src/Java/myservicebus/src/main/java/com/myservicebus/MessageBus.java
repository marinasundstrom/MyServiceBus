package com.myservicebus;

import java.io.IOException;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeFormatterBuilder;
import java.time.temporal.ChronoField;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.slf4j.Logger;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.JsonDeserializer;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;

public class MessageBus implements SendEndpoint, PublishEndpoint {
    private final ServiceProvider serviceProvider;
    private final TransportFactory transportFactory;
    private final SendEndpointProvider sendEndpointProvider;
    private final TransportSendEndpointProvider transportSendEndpointProvider;
    private final PublishPipe publishPipe;
    private final Logger logger;
    private final ObjectMapper mapper;
    private final List<ReceiveTransport> receiveTransports = new ArrayList<>();

    public MessageBus(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
        this.transportFactory = serviceProvider.getService(TransportFactory.class);
        this.sendEndpointProvider = serviceProvider.getService(SendEndpointProvider.class);
        this.transportSendEndpointProvider = serviceProvider.getService(TransportSendEndpointProvider.class);
        this.publishPipe = serviceProvider.getService(PublishPipe.class);
        this.logger = serviceProvider.getService(Logger.class);

        mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        mapper.enable(SerializationFeature.INDENT_OUTPUT);
        mapper.disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);

        JavaTimeModule module = new JavaTimeModule();
        DateTimeFormatter formatter = new DateTimeFormatterBuilder()
                .appendPattern("yyyy-MM-dd'T'HH:mm:ss")
                .appendFraction(ChronoField.NANO_OF_SECOND, 0, 6, true)
                .appendOffset("+HH:MM", "Z")
                .toFormatter();
        module.addDeserializer(OffsetDateTime.class, new JsonDeserializer<>() {
            @Override
            public OffsetDateTime deserialize(JsonParser p, DeserializationContext ctxt) throws IOException {
                return OffsetDateTime.parse(p.getText(), formatter);
            }
        });
        mapper.registerModule(module);
    }

    public static MessageBus configure(ServiceCollection services,
            java.util.function.Consumer<BusRegistrationConfigurator> configure) {
        var cfg = new BusRegistrationConfiguratorImpl(services);
        configure.accept(cfg);
        cfg.complete();
        return new MessageBus(services.build());
    }

    public void start() throws Exception {
        TopologyRegistry topology = serviceProvider.getService(TopologyRegistry.class);

        for (ConsumerTopology consumerDef : topology.getConsumers()) {
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
                    var type = mapper.getTypeFactory().constructParametricType(Envelope.class, binding.getMessageType());
                    Envelope<?> envelope = mapper.readValue(body, type);

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

        for (ReceiveTransport transport : receiveTransports) {
            transport.start();
        }
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
            SendEndpoint endpoint = sendEndpointProvider.getSendEndpoint(address);
            return endpoint.send(context).thenRun(() -> {
                logger.info("📤 Published message of type {}", context.getMessage().getClass().getSimpleName());
            });
        });
    }

    public <T> CompletableFuture<Void> publish(T message) {
        return publish(message, CancellationToken.none);
    }

    @Override
    public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
        if (message == null)
            return CompletableFuture.failedFuture(new IllegalArgumentException("Message cannot be null"));
        SendContext ctx = new SendContext(message, cancellationToken);
        return send(ctx);
    }

    @Override
    public CompletableFuture<Void> send(SendContext context) {
        String queue = NamingConventions.getQueueName(context.getMessage().getClass());
        String address = transportFactory.getSendAddress(queue);
        SendEndpoint endpoint = sendEndpointProvider.getSendEndpoint(address);
        return endpoint.send(context);
    }

    public <T> CompletableFuture<Void> send(T message) {
        return send(message, CancellationToken.none);
    }
}
