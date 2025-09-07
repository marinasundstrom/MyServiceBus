package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqSendContext;
import com.myservicebus.tasks.CancellationToken;

class RabbitMqSendContextTest {
    @Test
    void exposesBasicProperties() {
        RabbitMqSendContext ctx = new RabbitMqSendContext("hi", CancellationToken.none);
        assertEquals(2, ctx.getProperties().build().getDeliveryMode().intValue());
    }
}
