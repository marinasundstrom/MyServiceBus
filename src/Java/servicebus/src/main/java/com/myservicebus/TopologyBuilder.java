package com.myservicebus;

import com.rabbitmq.client.BuiltinExchangeType;

public interface TopologyBuilder {
    void declareExchange(String name, BuiltinExchangeType type, boolean durable);

    void declareQueue(String name, boolean durable, boolean autoDelete);

    void bindQueue(String queue, String exchange, String routingKey);
}