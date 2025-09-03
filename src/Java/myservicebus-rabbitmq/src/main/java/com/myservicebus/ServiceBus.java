package com.myservicebus;

import java.io.IOException;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeFormatterBuilder;
import java.time.temporal.ChronoField;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeoutException;

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
import com.myservicebus.rabbitmq.ConnectionProvider;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.ErrorTransportFilter;
import com.rabbitmq.client.BuiltinExchangeType;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;

public class ServiceBus implements SendEndpoint, PublishEndpoint {
    private final ServiceProvider serviceProvider;
    private final ConnectionProvider connectionProvider;
    private final SendEndpointProvider sendEndpointProvider;
    private final TransportSendEndpointProvider transportSendEndpointProvider;
    private final PublishPipe publishPipe;
    private final Logger logger;
    private Connection connection;
    private Channel channel;
    private ObjectMapper mapper;

    public ServiceBus(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
        this.connectionProvider = serviceProvider.getService(ConnectionProvider.class);
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

        module.addDeserializer(OffsetDateTime.class,
                new JsonDeserializer<>() {
                    @Override
                    public OffsetDateTime deserialize(JsonParser p, DeserializationContext ctxt) throws IOException {
                        return OffsetDateTime.parse(p.getText(), formatter);
                    }
                });

        mapper.registerModule(module);
    }

    public static ServiceBus configure(ServiceCollection services,
            java.util.function.Consumer<BusRegistrationConfigurator> configure) {
        var busRegistrationConfigurator = new BusRegistrationConfiguratorImpl(services);
        configure.accept(busRegistrationConfigurator);
        busRegistrationConfigurator.complete();
        return new ServiceBus(services.build());
    }

    public void start() throws IOException, TimeoutException {
        try {
            connection = connectionProvider.getOrCreateConnection();
            channel = connection.createChannel();
        } catch (Exception ex) {
            logger.error("Failed to start RabbitMQ connection", ex);
            throw new IOException("Failed to start RabbitMQ connection", ex);
        }

        TopologyRegistry topology = serviceProvider.getService(TopologyRegistry.class);

        for (ConsumerTopology consumerDef : topology.getConsumers()) {
            String queue = consumerDef.getQueueName();

            PipeConfigurator<ConsumeContext<Object>> configurator = new PipeConfigurator<>();
            @SuppressWarnings({"unchecked", "rawtypes"})
            Filter<ConsumeContext<Object>> errorFilter = new ErrorTransportFilter();
            configurator.useFilter(errorFilter);
            @SuppressWarnings({"unchecked", "rawtypes"})
            Filter<ConsumeContext<Object>> faultFilter =
                    new ConsumerFaultFilter(serviceProvider, consumerDef.getConsumerType());
            configurator.useFilter(faultFilter);
            configurator.useRetry(3);
            @SuppressWarnings({"unchecked", "rawtypes"})
            Filter<ConsumeContext<Object>> consumerFilter =
                    new ConsumerMessageFilter(serviceProvider, consumerDef.getConsumerType());
            configurator.useFilter(consumerFilter);
            if (consumerDef.getConfigure() != null)
                consumerDef.getConfigure().accept(configurator);
            Pipe<ConsumeContext<Object>> pipe = configurator.build();

            for (MessageBinding binding : consumerDef.getBindings()) {
                String exchangeName = binding.getEntityName();
                channel.exchangeDeclare(exchangeName, BuiltinExchangeType.FANOUT, true);
                channel.queueDeclare(queue, true, false, false, null);
                channel.queueBind(queue, exchangeName, "");
            }

            String errorExchange = queue + "_error";
            String errorQueue = errorExchange;
            channel.exchangeDeclare(errorExchange, BuiltinExchangeType.FANOUT, true);
            channel.queueDeclare(errorQueue, true, false, false, null);
            channel.queueBind(errorQueue, errorExchange, "");

            MessageBinding binding = consumerDef.getBindings().get(0);
            channel.basicConsume(queue, false, (tag, delivery) -> {
                try {
                    var type = mapper
                            .getTypeFactory()
                            .constructParametricType(Envelope.class, binding.getMessageType());

                    var envelope = (Envelope<?>) mapper.readValue(delivery.getBody(), type);

                    String faultAddress = envelope.getFaultAddress() != null
                            ? envelope.getFaultAddress()
                            : "rabbitmq://localhost/exchange/" + queue + "_error";

                    ConsumeContext<Object> ctx = new ConsumeContext<>(
                            envelope.getMessage(),
                            envelope.getHeaders(),
                            envelope.getResponseAddress(),
                            faultAddress,
                            CancellationToken.none,
                            transportSendEndpointProvider);

                    pipe.send(ctx).join();

                    channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                } catch (Exception e) {
                    try {
                        channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                    } catch (IOException ioEx) {
                        logger.error("Failed to ack message", ioEx);
                    }

                    logger.error("Message handler faulted", e);
                }
            }, tag -> {
            });

            logger.info("🚀 Service bus started. Listening on queue: {}", queue);
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
        return publishPipe.send(context).thenCompose(v -> {
            var endpoint = sendEndpointProvider.getSendEndpoint("rabbitmq://localhost/exchange/" + exchange);
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
            return CompletableFuture
                    .failedFuture(new IllegalArgumentException("Message cannot be null"));

        SendContext ctx = new SendContext(message, cancellationToken);
        return send(ctx);
    }

    @Override
    public CompletableFuture<Void> send(SendContext context) {
        String queue = NamingConventions.getQueueName(context.getMessage().getClass());
        var endpoint = sendEndpointProvider.getSendEndpoint("rabbitmq://localhost/" + queue);
        return endpoint.send(context);
    }

    public <T> CompletableFuture<Void> send(T message) {
        return send(message, CancellationToken.none);
    }

    public void stop() throws IOException, TimeoutException {
        if (channel != null && channel.isOpen())
            channel.close();
        if (connection != null && connection.isOpen())
            connection.close();
    }
}