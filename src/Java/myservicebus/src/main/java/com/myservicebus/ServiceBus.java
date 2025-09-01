package com.myservicebus;

import java.io.IOException;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeFormatterBuilder;
import java.time.temporal.ChronoField;
import java.util.concurrent.TimeoutException;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationContext;
import com.fasterxml.jackson.databind.JsonDeserializer;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.util.EnvelopeDeserializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.NamingConventions;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.rabbitmq.ConnectionProvider;
import com.rabbitmq.client.BuiltinExchangeType;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;

public class ServiceBus {
    private final ServiceProvider serviceProvider;
    private final ConnectionProvider connectionProvider;
    private final SendEndpointProvider sendEndpointProvider;
    private Connection connection;
    private Channel channel;
    private ObjectMapper mapper;

    public ServiceBus(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
        this.connectionProvider = serviceProvider.getService(ConnectionProvider.class);
        this.sendEndpointProvider = serviceProvider.getService(SendEndpointProvider.class);

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
            throw new IOException("Failed to start RabbitMQ connection", ex);
        }

        ConsumerRegistry registry = serviceProvider.getService(ConsumerRegistry.class);

        for (ConsumerDefinition<?, ?> def : registry.getAll()) {
            // String exchange = def.getExchangeName();
            String queue = def.getQueueName();

            String exchangeName = def.getExchangeName();

            channel.exchangeDeclare(exchangeName, BuiltinExchangeType.FANOUT, true);
            channel.queueDeclare(queue, true, false, false, null);
            channel.queueBind(queue, exchangeName, "");

            channel.basicConsume(queue, false, (tag, delivery) -> {
                try (var scope = serviceProvider.createScope()) {
                    var scopedServiceProvider = scope.getServiceProvider();

                    Consumer<Object> consumer = (Consumer<Object>) scopedServiceProvider
                            .getService(def.getConsumerType());
                    try {
                        var type = mapper
                                .getTypeFactory()
                                .constructParametricType(Envelope.class, def.getMessageType());

                        var envelope = (Envelope<?>) mapper.readValue(delivery.getBody(), type);

                        ConsumeContext<Object> ctx = new ConsumeContext<>(
                                envelope.getMessage(),
                                envelope.getHeaders(),
                                envelope.getResponseAddress(),
                                envelope.getFaultAddress(),
                                CancellationToken.none,
                                sendEndpointProvider);

                        consumer.consume(ctx).get();

                        channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                    } catch (Exception e) {
                        try {
                            channel.basicNack(delivery.getEnvelope().getDeliveryTag(), false, false);
                        } catch (IOException ioEx) {
                            ioEx.printStackTrace();
                        }

                        e.printStackTrace();
                    }
                }
            }, tag -> {
            });

            System.out.println("🚀 Service bus started. Listening on queue: " + queue);
        }
    }

    public void publish(Object message) throws IOException {
        if (message == null)
            throw new IllegalArgumentException("Message cannot be null");

        String exchange = NamingConventions.getExchangeName(message.getClass());
        var endpoint = sendEndpointProvider.getSendEndpoint("rabbitmq://localhost/" + exchange);
        try {
            endpoint.send(message, CancellationToken.none).join();
            System.out.println("📤 Published message of type " + message.getClass().getSimpleName());
        } catch (Exception ex) {
            throw new IOException("Failed to publish message", ex);
        }
    }

    public void stop() throws IOException, TimeoutException {
        if (channel != null && channel.isOpen())
            channel.close();
        if (connection != null && connection.isOpen())
            connection.close();
    }
}