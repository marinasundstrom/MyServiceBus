package com.myservicebus;

import java.time.OffsetDateTime;
import java.util.Collections;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

/**
 * Context passed to consumers when a message is received.
 *
 * <p>
 * Provides basic send and publish capabilities by delegating to an injected
 * {@link SendEndpointProvider}. The URI scheme used matches the RabbitMQ
 * implementation: publishes target an exchange URI while sends target a queue
 * URI.
 * </p>
 */
public class ConsumeContext<T>
        implements PipeContext,
        MessageConsumeContext,
        PublishEndpoint,
        SendEndpointProvider {

    private final T message;
    private final Map<String, Object> headers;
    private final String responseAddress;
    private final String faultAddress;
    private final CancellationToken cancellationToken;
    private final SendEndpointProvider sendEndpointProvider;

    public ConsumeContext(T message, Map<String, Object> headers, SendEndpointProvider provider) {
        this(message, headers, null, null, CancellationToken.none, provider);
    }

    public ConsumeContext(T message, Map<String, Object> headers, String responseAddress, String faultAddress,
            CancellationToken cancellationToken, SendEndpointProvider provider) {
        this.message = message;
        this.headers = headers;
        this.responseAddress = responseAddress;
        this.faultAddress = faultAddress;
        this.cancellationToken = cancellationToken;
        this.sendEndpointProvider = provider;
    }

    public T getMessage() {
        return message;
    }

    public Map<String, Object> getHeaders() {
        return headers;
    }

    @Override
    public <TMessage> CompletableFuture<Void> publish(TMessage message, CancellationToken cancellationToken) {
        String exchange = NamingConventions.getExchangeName(message.getClass());
        SendEndpoint endpoint = getSendEndpoint("rabbitmq://localhost/exchange/" + exchange);
        return endpoint.send(message, cancellationToken);
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        if (sendEndpointProvider == null) {
            throw new UnsupportedOperationException("SendEndpointProvider not configured");
        }
        return sendEndpointProvider.getSendEndpoint(uri);
    }

    public <TMessage> CompletableFuture<Void> respond(TMessage message, CancellationToken cancellationToken) {
        if (responseAddress == null) {
            return CompletableFuture.completedFuture(null);
        }
        SendEndpoint endpoint = getSendEndpoint(responseAddress);
        return endpoint.send(message, cancellationToken);
    }

    public CompletableFuture<Void> respondFault(Exception exception, CancellationToken cancellationToken) {
        String address = faultAddress != null ? faultAddress : responseAddress;
        if (address == null) {
            return CompletableFuture.completedFuture(null);
        }

        Fault<T> fault = new Fault<>();
        fault.setMessage(message);
        fault.setFaultId(UUID.randomUUID());
        fault.setSentTime(OffsetDateTime.now());
        fault.setHost(HostInfoProvider.capture());
        fault.setExceptions(Collections.singletonList(ExceptionInfo.fromException(exception)));

        Object id = headers.get("messageId");
        if (id instanceof UUID uuid) {
            fault.setMessageId(uuid);
        } else if (id instanceof String s) {
            try {
                fault.setMessageId(UUID.fromString(s));
            } catch (IllegalArgumentException ignored) {
            }
        }

        SendEndpoint endpoint = getSendEndpoint(address);
        return endpoint.send(fault, cancellationToken);
    }

    @Override
    public CancellationToken getCancellationToken() {
        return cancellationToken;
    }
}