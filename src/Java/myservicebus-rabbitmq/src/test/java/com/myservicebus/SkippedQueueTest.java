package com.myservicebus;

import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;
import static org.junit.jupiter.api.Assertions.*;

import java.util.concurrent.CompletableFuture;
import java.util.function.Function;
import java.io.IOException;

import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

import com.myservicebus.rabbitmq.RabbitMqReceiveTransport;
import com.myservicebus.TransportMessage;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.DeliverCallback;
import com.rabbitmq.client.Delivery;
import com.rabbitmq.client.Envelope;
import com.rabbitmq.client.CancelCallback;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.Slf4jLoggerFactory;

class SkippedQueueTest {
    @Test
    void movesUnknownMessagesToSkippedQueue() throws Exception {
        Channel channel = mock(Channel.class);
        ArgumentCaptor<DeliverCallback> captor = ArgumentCaptor.forClass(DeliverCallback.class);
        when(channel.basicConsume(eq("input"), eq(false), captor.capture(), any(CancelCallback.class))).thenReturn("tag");

        Function<TransportMessage, CompletableFuture<Void>> handler = tm -> CompletableFuture.completedFuture(null);

        var events = new java.util.ArrayList<BusHookEvent>();
        var hooks = new BusHookDispatcher(java.util.Set.of(events::add), null);
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqReceiveTransport transport = new RabbitMqReceiveTransport(channel, "input", handler, "fault", s -> false,
                loggerFactory, 1, hooks);
        transport.start();

        DeliverCallback callback = captor.getValue();
        AMQP.BasicProperties props = new AMQP.BasicProperties();
        byte[] body = "{\"messageId\":\"12345678-1234-1234-1234-123456789012\",\"messageType\":[\"urn:message:unknown\"],\"message\":{}}".getBytes();
        Envelope envelope = new Envelope(1L, false, "ex", "rk");
        Delivery delivery = new Delivery(envelope, props, body);
        callback.handle("tag", delivery);

        verify(channel).basicPublish(eq("input_skipped"), eq(""), eq(true), eq(props), eq(body));
        verify(channel).waitForConfirmsOrDie();
        verify(channel).basicAck(1L, false);
        var skipped = (MessageSkippedHookEvent) events.get(0);
        assertEquals("12345678-1234-1234-1234-123456789012", skipped.messageId());
        assertEquals(java.util.List.of("urn:message:unknown"), skipped.advertisedMessageUrns());
    }

    @Test
    void preservesMalformedBodyInErrorQueue() throws Exception {
        Channel channel = mock(Channel.class);
        ArgumentCaptor<DeliverCallback> captor = ArgumentCaptor.forClass(DeliverCallback.class);
        when(channel.basicConsume(eq("input"), eq(false), captor.capture(), any(CancelCallback.class))).thenReturn("tag");

        Function<TransportMessage, CompletableFuture<Void>> handler = tm -> CompletableFuture.completedFuture(null);

        var events = new java.util.ArrayList<BusHookEvent>();
        var hooks = new BusHookDispatcher(java.util.Set.of(events::add), null);
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqReceiveTransport transport = new RabbitMqReceiveTransport(channel, "input", handler, "fault", s -> true,
                loggerFactory, 1, hooks);
        transport.start();

        DeliverCallback callback = captor.getValue();
        AMQP.BasicProperties props = new AMQP.BasicProperties();
        byte[] body = "{bad-json".getBytes();
        Envelope envelope = new Envelope(1L, false, "ex", "rk");
        Delivery delivery = new Delivery(envelope, props, body);
        callback.handle("tag", delivery);

        verify(channel).basicPublish(eq("input_error"), eq(""), eq(true), any(AMQP.BasicProperties.class), eq(body));
        verify(channel).waitForConfirmsOrDie();
        verify(channel).basicAck(1L, false);
        assertTrue(events.isEmpty());
    }

    @Test
    void nacksUnknownMessageWhenSkippedMoveIsNotConfirmed() throws Exception {
        Channel channel = mock(Channel.class);
        ArgumentCaptor<DeliverCallback> captor = ArgumentCaptor.forClass(DeliverCallback.class);
        when(channel.basicConsume(eq("input"), eq(false), captor.capture(), any(CancelCallback.class)))
                .thenReturn("tag");
        doThrow(new IOException("confirm failed")).when(channel).waitForConfirmsOrDie();

        Function<TransportMessage, CompletableFuture<Void>> handler = tm -> CompletableFuture.completedFuture(null);
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqReceiveTransport transport = new RabbitMqReceiveTransport(
                channel, "input", handler, "fault", s -> false, loggerFactory);
        transport.start();

        DeliverCallback callback = captor.getValue();
        AMQP.BasicProperties props = new AMQP.BasicProperties();
        byte[] body = "{\"messageType\":[\"urn:message:test\"],\"message\":{}}".getBytes();
        Envelope envelope = new Envelope(1L, false, "ex", "rk");
        callback.handle("tag", new Delivery(envelope, props, body));

        verify(channel, never()).basicAck(anyLong(), anyBoolean());
        verify(channel).basicNack(1L, false, true);
    }
}
