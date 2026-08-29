package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.lang.reflect.Proxy;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.atomic.AtomicReference;
import java.io.IOException;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqSendTransport;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.mock;

public class TransportHeaderTest {

    @Test
    public void underscoreHeadersAppliedToBasicProperties() throws Exception {
        AtomicReference<AMQP.BasicProperties> captured = new AtomicReference<>();
        Channel channel = (Channel) Proxy.newProxyInstance(
                Channel.class.getClassLoader(),
                new Class[] { Channel.class },
                (proxy, method, args) -> {
                    if ("basicPublish".equals(method.getName())) {
                        for (Object argument : args) {
                            if (argument instanceof AMQP.BasicProperties properties) {
                                captured.set(properties);
                            }
                        }
                    }
                    return null;
                });

        RabbitMqSendTransport transport = new RabbitMqSendTransport(channel, "", "test");

        Map<String, Object> headers = new HashMap<>();
        headers.put("_correlation_id", "123");

        transport.send(new byte[0], headers, "application/json");

        AMQP.BasicProperties props = captured.get();
        assertNotNull(props);
        assertEquals("123", props.getCorrelationId());
        assertEquals(2, props.getDeliveryMode());
        assertTrue(props.getHeaders() == null || !props.getHeaders().containsKey("correlation_id"));
    }

    @Test
    public void publisherRejectionFailsTheSend() throws Exception {
        Channel channel = mock(Channel.class);
        doThrow(new IOException("nack")).when(channel).waitForConfirmsOrDie();
        RabbitMqSendTransport transport = new RabbitMqSendTransport(channel, "", "orders", true);

        RuntimeException exception = assertThrows(
                RuntimeException.class,
                () -> transport.send(new byte[0], Map.of(), "application/json"));

        assertTrue(exception.getMessage().contains("Failed to send message"));
        org.mockito.Mockito.verify(channel).basicPublish(
                anyString(), anyString(), eq(true), any(AMQP.BasicProperties.class), any(byte[].class));
    }
}
