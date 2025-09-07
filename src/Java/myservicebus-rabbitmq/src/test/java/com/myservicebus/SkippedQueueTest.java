package com.myservicebus;

import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;
import static org.junit.jupiter.api.Assertions.*;

import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

import com.myservicebus.rabbitmq.RabbitMqReceiveTransport;
import com.myservicebus.TransportMessage;
import com.myservicebus.UnknownMessageTypeException;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.DeliverCallback;
import com.rabbitmq.client.Delivery;
import com.rabbitmq.client.Envelope;
import com.rabbitmq.client.CancelCallback;

class SkippedQueueTest {
    @Test
    void movesUnknownMessagesToSkippedQueue() throws Exception {
        Channel channel = mock(Channel.class);
        ArgumentCaptor<DeliverCallback> captor = ArgumentCaptor.forClass(DeliverCallback.class);
        when(channel.basicConsume(eq("input"), eq(false), captor.capture(), any(CancelCallback.class))).thenReturn("tag");

        Function<TransportMessage, CompletableFuture<Void>> handler = tm -> {
            CompletableFuture<Void> f = new CompletableFuture<>();
            f.completeExceptionally(new UnknownMessageTypeException(null));
            return f;
        };

        RabbitMqReceiveTransport transport = new RabbitMqReceiveTransport(channel, "input", handler, "fault");
        transport.start();

        DeliverCallback callback = captor.getValue();
        AMQP.BasicProperties props = new AMQP.BasicProperties();
        byte[] body = new byte[0];
        Envelope envelope = new Envelope(1L, false, "ex", "rk");
        Delivery delivery = new Delivery(envelope, props, body);
        callback.handle("tag", delivery);

        verify(channel).basicPublish(eq("input_skipped"), eq(""), eq(props), eq(body));
        verify(channel).basicAck(1L, false);
    }
}
