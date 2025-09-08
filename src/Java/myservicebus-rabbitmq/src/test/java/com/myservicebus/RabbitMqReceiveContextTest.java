package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.util.Map;
import java.util.UUID;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqReceiveContext;
import com.rabbitmq.client.AMQP;

class RabbitMqReceiveContextTest {
    @Test
    void mapsRabbitMqFieldsToQueueContext() {
        Map<String, Object> headers = Map.of("foo", "bar");
        AMQP.BasicProperties props = new AMQP.BasicProperties.Builder()
                .messageId(UUID.randomUUID().toString())
                .headers(headers)
                .replyTo("queue")
                .build();
        RabbitMqReceiveContext ctx = new RabbitMqReceiveContext(props, 42L, "exchange", "rk");
        assertEquals(42L, ctx.getDeliveryCount());
        assertEquals("exchange", ctx.getDestination());
        assertEquals(headers, ctx.getBrokerProperties());
        assertEquals(headers, ctx.getHeaders());
    }
}
