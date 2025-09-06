package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.lang.reflect.Proxy;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqSendEndpoint;
import com.myservicebus.rabbitmq.RabbitMqSendTransport;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;

public class RabbitMqHeaderEncodingTest {
    @Test
    void convertsStringHeadersToBytes() throws Exception {
        AtomicReference<AMQP.BasicProperties> captured = new AtomicReference<>();
        Channel channel = (Channel) Proxy.newProxyInstance(
                Channel.class.getClassLoader(),
                new Class[] { Channel.class },
                (proxy, method, args) -> {
                    if ("basicPublish".equals(method.getName()) && args.length >= 4) {
                        captured.set((AMQP.BasicProperties) args[2]);
                    }
                    return null;
                });

        RabbitMqSendTransport transport = new RabbitMqSendTransport(channel, "", "test");
        RabbitMqSendEndpoint endpoint = new RabbitMqSendEndpoint(transport, new EnvelopeMessageSerializer());

        class Dummy { public String text = "hi"; }
        SendContext ctx = new SendContext(new Dummy(), CancellationToken.none);
        ctx.getHeaders().put(MessageHeaders.HOST_MACHINE, "machine");
        ctx.getHeaders().put("content_type", "application/vnd.masstransit+json");

        endpoint.send(ctx).join();

        AMQP.BasicProperties props = captured.get();
        assertNotNull(props);
        assertTrue(props.getHeaders().containsKey(MessageHeaders.HOST_MACHINE));
        assertArrayEquals("machine".getBytes(StandardCharsets.UTF_8),
                (byte[]) props.getHeaders().get(MessageHeaders.HOST_MACHINE));
    }
}
