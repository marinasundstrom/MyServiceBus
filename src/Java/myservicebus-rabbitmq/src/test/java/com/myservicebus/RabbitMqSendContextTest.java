package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqSendContext;
import com.myservicebus.tasks.CancellationToken;
import java.time.Duration;

class RabbitMqSendContextTest {
    @Test
    void exposesBasicProperties() {
        RabbitMqSendContext ctx = new RabbitMqSendContext("hi", CancellationToken.none);
        assertEquals(2, ctx.getProperties().build().getDeliveryMode().intValue());
    }

    @Test
    void queueSettingsApplied() {
        RabbitMqSendContext ctx = new RabbitMqSendContext("hi", CancellationToken.none);
        ctx.setTimeToLive(Duration.ofSeconds(5));
        ctx.setPersistent(false);
        ctx.getBrokerProperties().put("x-priority", 5);

        var props = ctx.getProperties().build();
        assertEquals("5000", props.getExpiration());
        assertEquals(1, props.getDeliveryMode().intValue());
        assertEquals(5, props.getHeaders().get("x-priority"));
    }
}
