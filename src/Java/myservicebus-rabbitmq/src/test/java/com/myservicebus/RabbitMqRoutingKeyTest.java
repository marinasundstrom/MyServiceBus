package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.util.Map;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqSendEndpoint;
import com.myservicebus.SendTransport;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;

public class RabbitMqRoutingKeyTest {
    @Test
    public void routingKeyAddedAsHeader() throws Exception {
        AtomicReference<Map<String,Object>> captured = new AtomicReference<>();
        SendTransport transport = new SendTransport() {
            @Override
            public void send(byte[] data, Map<String, Object> headers, String contentType) {
                captured.set(headers);
            }
        };
        RabbitMqSendEndpoint endpoint = new RabbitMqSendEndpoint(transport, new EnvelopeMessageSerializer());
        QueueSendContext ctx = new QueueSendContext("hi", CancellationToken.none);
        ctx.setRoutingKey("order");
        endpoint.send(ctx).get();
        assertEquals("order", captured.get().get("_routing_key"));
    }
}

