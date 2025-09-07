package com.myservicebus;

import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.Slf4jLoggerFactory;
import com.myservicebus.rabbitmq.RabbitMqReceiveTransport;
import com.myservicebus.TransportMessage;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.DeliverCallback;
import com.rabbitmq.client.Delivery;
import com.rabbitmq.client.Envelope;
import com.rabbitmq.client.CancelCallback;

class ErrorHandlingTest {
    @Test
    void nacksWhenHandlerFails() throws Exception {
        Channel channel = mock(Channel.class);
        ArgumentCaptor<DeliverCallback> captor = ArgumentCaptor.forClass(DeliverCallback.class);
        when(channel.basicConsume(eq("input"), eq(false), captor.capture(), any(CancelCallback.class))).thenReturn("tag");

        Function<TransportMessage, CompletableFuture<Void>> handler = tm -> {
            CompletableFuture<Void> cf = new CompletableFuture<>();
            cf.completeExceptionally(new RuntimeException("boom"));
            return cf;
        };

        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqReceiveTransport transport = new RabbitMqReceiveTransport(channel, "input", handler, "fault", s -> true, loggerFactory);
        transport.start();

        DeliverCallback callback = captor.getValue();
        AMQP.BasicProperties props = new AMQP.BasicProperties();
        byte[] body = "{\"messageType\":[\"urn:message:test\"],\"message\":{}}".getBytes();
        Envelope envelope = new Envelope(1L, false, "ex", "rk");
        Delivery delivery = new Delivery(envelope, props, body);
        callback.handle("tag", delivery);

        verify(channel, timeout(1000)).basicNack(1L, false, true);
        verify(channel, never()).basicAck(anyLong(), anyBoolean());
    }
}
