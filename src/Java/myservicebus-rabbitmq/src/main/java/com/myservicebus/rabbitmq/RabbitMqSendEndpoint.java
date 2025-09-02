package com.myservicebus.rabbitmq;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.HostInfoProvider;
import com.myservicebus.NamingConventions;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendTransport;
import com.myservicebus.tasks.CancellationToken;

/**
 * Send endpoint that serializes messages into envelopes and publishes them via RabbitMQ.
 */
public class RabbitMqSendEndpoint implements SendEndpoint {
    private final SendTransport transport;
    private final String exchange;
    private final ObjectMapper mapper;

    public RabbitMqSendEndpoint(SendTransport transport, String exchange, ObjectMapper mapper) {
        this.transport = transport;
        this.exchange = exchange;
        this.mapper = mapper;
    }

    @Override
    public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
        try {
            Envelope<T> envelope = new Envelope<>();
            envelope.setMessageId(UUID.randomUUID());
            envelope.setConversationId(UUID.randomUUID());
            envelope.setSentTime(OffsetDateTime.now());
            envelope.setDestinationAddress("rabbitmq://localhost/" + exchange);
            envelope.setMessageType(List.of(NamingConventions.getMessageUrn(message.getClass())));
            envelope.setMessage(message);
            envelope.setHeaders(Map.of());
            envelope.setContentType("application/json");
            envelope.setHost(HostInfoProvider.capture());

            byte[] body = mapper.writeValueAsBytes(envelope);
            transport.send(body);
            return CompletableFuture.completedFuture(null);
        } catch (Exception e) {
            CompletableFuture<Void> future = new CompletableFuture<>();
            future.completeExceptionally(e);
            return future;
        }
    }
}
