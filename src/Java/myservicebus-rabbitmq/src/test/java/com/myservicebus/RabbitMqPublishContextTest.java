package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqPublishContext;
import com.myservicebus.tasks.CancellationToken;

class RabbitMqPublishContextTest {
    @Test
    void exposesBasicProperties() {
        RabbitMqPublishContext ctx = new RabbitMqPublishContext("hi", CancellationToken.none);
        assertEquals(2, ctx.getProperties().build().getDeliveryMode().intValue());
    }
}
