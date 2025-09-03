package com.myservicebus.rabbitmq;

import com.myservicebus.util.EnvelopeDeserializer;
import com.rabbitmq.client.*;

import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;

public class SimpleConsumer {
    public static void main2(String[] args) throws Exception {
        ConnectionFactory factory = new ConnectionFactory();
        factory.setHost("localhost");

        Connection connection = factory.newConnection();
        Channel channel = connection.createChannel();

        String queueName = "my-service-queue";
        String errorExchange = queueName + "_error";
        String errorQueue = queueName + "_error";

        channel.exchangeDeclare(errorExchange, "fanout", true);
        channel.queueDeclare(errorQueue, true, false, false, null);
        channel.queueBind(errorQueue, errorExchange, "");

        Map<String, Object> queueArgs = new HashMap<>();
        queueArgs.put("x-dead-letter-exchange", errorExchange);
        channel.queueDeclare(queueName, true, false, false, queueArgs);

        DeliverCallback deliverCallback = (consumerTag, delivery) -> {
            String json = new String(delivery.getBody(), StandardCharsets.UTF_8);
            System.out.println("📨 Received message:");
            System.out.println(json);

            try {
                Object result = EnvelopeDeserializer.deserializeAndUnwrapFault(json);
                System.out.println("✅ Payload: " + result);
                channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
            } catch (Exception e) {
                channel.basicNack(delivery.getEnvelope().getDeliveryTag(), false, false);
                e.printStackTrace();
            }
        };

        channel.basicConsume(queueName, false, deliverCallback, consumerTag -> {
        });
    }
}