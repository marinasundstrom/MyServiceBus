package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
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
    void nacksForRedeliveryWhenHandlerFailsBeforeErrorMove() throws Exception {
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

    @Test
    void acksWhenFailedMessageWasMovedToErrorQueue() throws Exception {
        Channel channel = mock(Channel.class);
        ArgumentCaptor<DeliverCallback> captor = ArgumentCaptor.forClass(DeliverCallback.class);
        when(channel.basicConsume(eq("input"), eq(false), captor.capture(), any(CancelCallback.class)))
                .thenReturn("tag");

        RuntimeException failure = new RuntimeException("boom");
        ErrorTransportSettlement.markMoved(failure, "rabbitmq://localhost/exchange/input_error");
        Function<TransportMessage, CompletableFuture<Void>> handler = tm -> CompletableFuture.failedFuture(failure);

        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqReceiveTransport transport = new RabbitMqReceiveTransport(
                channel, "input", handler, "fault", s -> true, loggerFactory);
        transport.start();

        DeliverCallback callback = captor.getValue();
        AMQP.BasicProperties props = new AMQP.BasicProperties();
        byte[] body = "{\"messageType\":[\"urn:message:test\"],\"message\":{}}".getBytes();
        Envelope envelope = new Envelope(1L, false, "ex", "rk");
        callback.handle("tag", new Delivery(envelope, props, body));

        verify(channel, timeout(1000)).basicAck(1L, false);
        verify(channel, never()).basicNack(anyLong(), anyBoolean(), anyBoolean());
    }

    @Test
    void stopCancelsNewDeliveriesAndWaitsForActiveDelivery() throws Exception {
        Channel channel = mock(Channel.class);
        when(channel.isOpen()).thenReturn(true);
        ArgumentCaptor<DeliverCallback> captor = ArgumentCaptor.forClass(DeliverCallback.class);
        when(channel.basicConsume(eq("input"), eq(false), captor.capture(), any(CancelCallback.class)))
                .thenReturn("consumer-tag");

        CountDownLatch handlerStarted = new CountDownLatch(1);
        CompletableFuture<Void> releaseHandler = new CompletableFuture<>();
        Function<TransportMessage, CompletableFuture<Void>> handler = message -> {
            handlerStarted.countDown();
            return releaseHandler;
        };
        RabbitMqReceiveTransport transport = new RabbitMqReceiveTransport(
                channel,
                "input",
                handler,
                "fault",
                ignored -> true,
                new Slf4jLoggerFactory());
        transport.start();

        byte[] body = "{\"messageType\":[\"urn:message:test\"],\"message\":{}}".getBytes();
        Delivery delivery = new Delivery(
                new Envelope(1L, false, "ex", "rk"),
                new AMQP.BasicProperties(),
                body);
        captor.getValue().handle("consumer-tag", delivery);
        assertTrue(handlerStarted.await(1, TimeUnit.SECONDS));

        CompletableFuture<Void> stop = CompletableFuture.runAsync(() -> {
            try {
                transport.stop();
            } catch (Exception exception) {
                throw new RuntimeException(exception);
            }
        });
        verify(channel, timeout(1000)).basicCancel("consumer-tag");
        assertFalse(stop.isDone());

        releaseHandler.complete(null);
        stop.join();
        verify(channel).basicAck(1L, false);
        verify(channel).close();
    }
}
