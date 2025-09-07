package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqReceiveContext;
import com.rabbitmq.client.AMQP;

class RabbitMqReceiveContextTest {
    @Test
    void storesMetadata() {
        AMQP.BasicProperties props = new AMQP.BasicProperties.Builder().build();
        RabbitMqReceiveContext ctx = new RabbitMqReceiveContext(props, 42L, "ex", "rk");
        assertEquals(props, ctx.getProperties());
        assertEquals(42L, ctx.getDeliveryTag());
        assertEquals("ex", ctx.getExchange());
        assertEquals("rk", ctx.getRoutingKey());
    }
}
