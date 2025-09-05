package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.lang.reflect.Proxy;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqSendTransport;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;

public class TransportHeaderTest {

    @Test
    public void underscoreHeadersAppliedToBasicProperties() throws Exception {
        AtomicReference<AMQP.BasicProperties> captured = new AtomicReference<>();
        Channel channel = (Channel) Proxy.newProxyInstance(
                Channel.class.getClassLoader(),
                new Class[] { Channel.class },
                (proxy, method, args) -> {
                    if ("basicPublish".equals(method.getName()) && args.length >= 3)
                        captured.set((AMQP.BasicProperties) args[2]);
                    return null;
                });

        RabbitMqSendTransport transport = new RabbitMqSendTransport(channel, "", "test");

        Map<String, Object> headers = new HashMap<>();
        headers.put("_correlation_id", "123");

        transport.send(new byte[0], headers, "application/json");

        AMQP.BasicProperties props = captured.get();
        assertNotNull(props);
        assertEquals("123", props.getCorrelationId());
        assertTrue(props.getHeaders() == null || !props.getHeaders().containsKey("correlation_id"));
    }
}

