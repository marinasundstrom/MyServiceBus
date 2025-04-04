package com.myservicebus.rabbitmq;

import com.myservicebus.util.EnvelopeDeserializer;
import com.rabbitmq.client.*;

import java.nio.charset.StandardCharsets;

public class SimpleConsumer {
    public static void main(String[] args) throws Exception {
        ConnectionFactory factory = new ConnectionFactory();
        factory.setHost("localhost");

        Connection connection = factory.newConnection();
        Channel channel = connection.createChannel();

        String queueName = "my-service-queue";
        channel.queueDeclare(queueName, true, false, false, null);

        DeliverCallback deliverCallback = (consumerTag, delivery) -> {
            String json = new String(delivery.getBody(), StandardCharsets.UTF_8);
            System.out.println("📨 Received message:");
            System.out.println(json);

            // TODO: Deserialize and process

            try {
                Object result = EnvelopeDeserializer.deserializeAndUnwrapFault(json);
                System.out.println("✅ Payload: " + result);
            } catch (Exception e) {
                // TODO Auto-generated catch block
                e.printStackTrace();
            }
        };

        channel.basicConsume(queueName, true, deliverCallback, consumerTag -> {
        });
    }
}