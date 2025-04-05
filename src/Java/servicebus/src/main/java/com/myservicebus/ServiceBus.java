package com.myservicebus;

import java.io.IOException;
import java.lang.reflect.InvocationTargetException;
import java.nio.charset.StandardCharsets;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.TimeoutException;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.util.EnvelopeDeserializer;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;
import com.rabbitmq.client.ConnectionFactory;
import com.rabbitmq.client.DeliverCallback;

public class ServiceBus {
    private final ServiceProvider serviceProvider;

    public ServiceBus(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
    }

    public static ServiceBus configure(ServiceCollection services,
            java.util.function.Consumer<BusRegistrationConfigurator> configure) {
        var busRegistrationConfigurator = new BusRegistrationConfiguratorImpl(services);
        configure.accept(busRegistrationConfigurator);
        busRegistrationConfigurator.complete();
        return new ServiceBus(services.build());
    }

    public void start() throws IOException, TimeoutException {
        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        mapper.enable(SerializationFeature.INDENT_OUTPUT);
        mapper.disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        mapper.registerModule(new JavaTimeModule());

        ConnectionFactory factory = new ConnectionFactory();
        factory.setHost("localhost");

        Connection connection = factory.newConnection();
        Channel channel = connection.createChannel();

        ConsumerRegistry registry = serviceProvider.getService(ConsumerRegistry.class);

        for (ConsumerDefinition<?, ?> def : registry.getAll()) {
            String exchange = def.getExchangeName();
            String queue = def.getQueueName();

            channel.exchangeDeclare(exchange, "fanout", true);
            channel.queueDeclare(queue, true, false, false, null);
            channel.queueBind(queue, exchange, "");

            channel.basicConsume(queue, true, (tag, delivery) -> {
                try (ServiceScope scope = serviceProvider.createScope()) {
                    Object consumer = scope.getService(def.getConsumerType());
                    Object message = EnvelopeDeserializer.deserialize(delivery.getBody(), def.getMessageType());

                    ConsumeContext<?> ctx = new ConsumeContext<>(message, null);
                    try {
                        consumer.getClass().getMethod("consume", ConsumeContext.class).invoke(consumer, ctx);
                    } catch (Exception e) {
                        // TODO Auto-generated catch block
                        e.printStackTrace();
                    }
                }
            }, tag -> {
            });

            System.out.println("🚀 Service bus started. Listening on queue: " + queue);
        }
    }

    public void sendTestMessage(Channel channel, ObjectMapper mapper) throws IOException {
        /*
         * SubmitOrder message = new SubmitOrder(UUID.randomUUID());
         * 
         * Envelope<SubmitOrder> envelope = new Envelope<>();
         * envelope.setMessageId(UUID.randomUUID());
         * envelope.setSentTime(OffsetDateTime.now());
         * envelope.setSourceAddress("rabbitmq://localhost/source");
         * envelope.setDestinationAddress("rabbitmq://localhost/destination");
         * envelope.setMessageType(List.of("urn:message:com.myservicebus:SubmitOrder"));
         * envelope.setMessage(message);
         * envelope.setHeaders(Map.of("Application-Header", "SomeValue"));
         * envelope.setContentType("application/json");
         * 
         * String json = mapper.writeValueAsString(envelope);
         * channel.basicPublish("", "my-service-queue", null,
         * json.getBytes(StandardCharsets.UTF_8));
         * System.out.println("📤 Sent test message");
         */
    }
}