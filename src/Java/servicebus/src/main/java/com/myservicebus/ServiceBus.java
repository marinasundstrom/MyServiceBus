package com.myservicebus;

import java.io.IOException;
import java.net.InetAddress;
import java.nio.charset.StandardCharsets;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeFormatterBuilder;
import java.time.temporal.ChronoField;
import java.util.List;
import java.util.Map;
import java.util.UUID;
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
import com.rabbitmq.client.BuiltinExchangeType;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;
import com.rabbitmq.client.ConnectionFactory;
import com.rabbitmq.client.DeliverCallback;
import com.rabbitmq.client.impl.AMQImpl.Basic.Consume;

public class ServiceBus {
    private final ServiceProvider serviceProvider;
    private Channel channel;
    private ObjectMapper mapper;

    public ServiceBus(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;

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
        ConnectionFactory factory = new ConnectionFactory();
        factory.setHost("localhost");

        Connection connection = factory.newConnection();
        channel = connection.createChannel();

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

                        ConsumeContext<Object> ctx = new ConsumeContext<>(envelope.getMessage(), null);

                        consumer.consume(ctx);

                        channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                    } catch (Exception e) {
                        channel.basicNack(delivery.getEnvelope().getDeliveryTag(), false, false);

                        // TODO Auto-generated catch block
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

        Class<?> messageType = message.getClass();

        Envelope<Object> envelope = new Envelope<>();
        envelope.setMessageId(UUID.randomUUID());
        envelope.setConversationId(UUID.randomUUID());
        envelope.setSentTime(OffsetDateTime.now());
        envelope.setSourceAddress("rabbitmq://localhost/source");
        envelope.setDestinationAddress("rabbitmq://localhost/" + NamingConventions.getMessageName(messageType));
        envelope.setMessageType(List.of(NamingConventions.getMessageUrn(messageType)));
        envelope.setMessage(message);
        envelope.setHeaders(Map.of());
        envelope.setContentType("application/json");

        String machineName = InetAddress.getLocalHost().getHostName();
        String processName = "java";
        long processId = ProcessHandle.current().pid(); // Java 9+
        String serviceName = "my-app";
        String serviceVersion = "1.0.0";
        String frameworkVersion = System.getProperty("java.version");
        String platform = "8.0.10.0"; // define or detect if needed
        String operatingSystemVersion = System.getProperty("os.name") + " " + System.getProperty("os.version");

        HostInfo host = new HostInfo(
                machineName,
                processName,
                (int) processId,
                serviceName,
                serviceVersion,
                frameworkVersion,
                platform,
                operatingSystemVersion);

        envelope.setHost(host);

        String json = mapper.writeValueAsString(envelope);
        byte[] body = json.getBytes(StandardCharsets.UTF_8);

        // messageType.getPackageName()

        String exchangeName = NamingConventions.getExchangeName(messageType);

        // Ensure the exchange exists (fanout-type)
        channel.exchangeDeclare(exchangeName, BuiltinExchangeType.FANOUT, true);

        // Publish to the exchange (fanout -> routingKey is ignored)
        channel.basicPublish(exchangeName, "", null, body);

        System.out.println("📤 Published message of type " + messageType.getSimpleName());
    }
}