package com.myservicebus.rabbitmq;

import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;
import com.rabbitmq.client.ConnectionFactory;

public class SimplePublisher {
    public static void main2(String[] args) throws Exception {
        ConnectionFactory factory = new ConnectionFactory();
        factory.setHost("localhost"); // or RabbitMQ server hostname
        try (Connection connection = factory.newConnection();
                Channel channel = connection.createChannel()) {

            String queueName = "my-service-queue";
            channel.queueDeclare(queueName, true, false, false, null);

            String json = "{\"messageId\":\"1234\",\"messageType\":[\"...\"],\"message\":{\"text\":\"Hello\"}}";

            channel.basicPublish("", queueName, null, json.getBytes());
            System.out.println("✅ Sent message");
        }
    }
}
