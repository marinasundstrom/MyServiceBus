package com.myservicebus;

import java.io.IOException;

import com.rabbitmq.client.BuiltinExchangeType;
import com.rabbitmq.client.Channel;

public class RabbitMqTopologyBuilder implements TopologyBuilder {
    private final Channel channel;

    public RabbitMqTopologyBuilder(Channel channel) {
        this.channel = channel;
    }

    @Override
    public void declareExchange(String name, BuiltinExchangeType type, boolean durable) {
        try {
            channel.exchangeDeclare(name, type, durable);
        } catch (IOException e) {
            // TODO Auto-generated catch block
            e.printStackTrace();
        }
    }

    @Override
    public void declareQueue(String name, boolean durable, boolean autoDelete) {
        try {
            channel.queueDeclare(name, durable, false, autoDelete, null);
        } catch (IOException e) {
            // TODO Auto-generated catch block
            e.printStackTrace();
        }
    }

    @Override
    public void bindQueue(String queue, String exchange, String routingKey) {
        try {
            channel.queueBind(queue, exchange, routingKey);
        } catch (IOException e) {
            // TODO Auto-generated catch block
            e.printStackTrace();
        }
    }
}