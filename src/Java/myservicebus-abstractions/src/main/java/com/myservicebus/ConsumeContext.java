package com.myservicebus;

import java.time.OffsetDateTime;
import java.time.Duration;
import java.time.Instant;
import java.util.Collections;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;
import java.net.URI;

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
    private final String errorAddress;
    private final CancellationToken cancellationToken;
    private final SendEndpointProvider sendEndpointProvider;
    private final URI busAddress;

    public ConsumeContext(T message, Map<String, Object> headers, SendEndpointProvider provider) {
        this(message, headers, null, null, null, CancellationToken.none, provider, URI.create("loopback://localhost/"));
    }

    public ConsumeContext(T message, Map<String, Object> headers, SendEndpointProvider provider, URI busAddress) {
        this(message, headers, null, null, null, CancellationToken.none, provider, busAddress);
    }

    public ConsumeContext(T message, Map<String, Object> headers, String responseAddress, String faultAddress,
            CancellationToken cancellationToken, SendEndpointProvider provider) {
        this(message, headers, responseAddress, faultAddress, null, cancellationToken, provider, URI.create("loopback://localhost/"));
    }

    public ConsumeContext(T message, Map<String, Object> headers, String responseAddress, String faultAddress,
            CancellationToken cancellationToken, SendEndpointProvider provider, URI busAddress) {
        this(message, headers, responseAddress, faultAddress, null, cancellationToken, provider, busAddress);
    }

    public ConsumeContext(T message, Map<String, Object> headers, String responseAddress, String faultAddress,
            String errorAddress, CancellationToken cancellationToken, SendEndpointProvider provider, URI busAddress) {
        this.message = message;
        this.headers = headers;
        this.responseAddress = responseAddress;
        this.faultAddress = faultAddress;
        this.errorAddress = errorAddress;
        this.cancellationToken = cancellationToken;
        this.sendEndpointProvider = provider;
        this.busAddress = busAddress;
    }

    public T getMessage() {
        return message;
    }

    public Map<String, Object> getHeaders() {
        return headers;
    }

    public String getFaultAddress() {
        return faultAddress;
    }

    public String getErrorAddress() {
        return errorAddress;
    }

    @Override
    public <TMessage> CompletableFuture<Void> publish(TMessage message, CancellationToken cancellationToken) {
        PublishContext ctx = new PublishContext(message, cancellationToken);
        return publish(ctx);
    }

    @Override
    public CompletableFuture<Void> publish(PublishContext context) {
        String exchange = EntityNameFormatter.format(context.getMessage().getClass());
        URI dest = busAddress.resolve("exchange/" + exchange);
        context.setSourceAddress(busAddress);
        context.setDestinationAddress(dest);
        SendEndpoint endpoint = getSendEndpoint(dest.toString());
        return endpoint.send(context);
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        if (sendEndpointProvider == null) {
            throw new UnsupportedOperationException("SendEndpointProvider not configured");
        }
        return sendEndpointProvider.getSendEndpoint(uri);
    }

    public <TMessage> CompletableFuture<Void> respond(TMessage message, CancellationToken cancellationToken) {
        SendContext ctx = new SendContext(message, cancellationToken);
        return respond(ctx);
    }

    public <TMessage> CompletableFuture<Void> respond(TMessage message) {
        return respond(message, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> respond(Class<TMessage> messageType, Object message,
            CancellationToken cancellationToken) {
        TMessage proxy = MessageProxy.create(messageType, message);
        return respond(proxy, cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> respond(Class<TMessage> messageType, Object message) {
        return respond(messageType, message, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> respond(TMessage message, Consumer<SendContext> contextCallback,
            CancellationToken cancellationToken) {
        SendContext ctx = new SendContext(message, cancellationToken);
        contextCallback.accept(ctx);
        return respond(ctx);
    }

    public <TMessage> CompletableFuture<Void> respond(TMessage message, Consumer<SendContext> contextCallback) {
        return respond(message, contextCallback, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> respond(Class<TMessage> messageType, Object message,
            Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
        TMessage proxy = MessageProxy.create(messageType, message);
        return respond(proxy, contextCallback, cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> respond(Class<TMessage> messageType, Object message,
            Consumer<SendContext> contextCallback) {
        return respond(messageType, message, contextCallback, CancellationToken.none);
    }

    @Override
    public CompletableFuture<Void> respond(SendContext context) {
        if (responseAddress == null) {
            return CompletableFuture.completedFuture(null);
        }
        context.setSourceAddress(busAddress);
        context.setDestinationAddress(URI.create(responseAddress));
        SendEndpoint endpoint = getSendEndpoint(responseAddress);
        return endpoint.send(context);
    }

    public <TMessage> CompletableFuture<Void> send(String destination, TMessage message, CancellationToken cancellationToken) {
        SendEndpoint endpoint = getSendEndpoint(destination);
        return endpoint.send(message, cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> send(String destination, TMessage message,
            Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
        SendEndpoint endpoint = getSendEndpoint(destination);
        return endpoint.send(message, contextCallback, cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> send(String destination, TMessage message,
            Consumer<SendContext> contextCallback) {
        return send(destination, message, contextCallback, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> send(String destination, TMessage message) {
        return send(destination, message, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> send(String destination, Class<TMessage> messageType, Object message,
            CancellationToken cancellationToken) {
        SendEndpoint endpoint = getSendEndpoint(destination);
        return endpoint.send(messageType, message, cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> send(String destination, Class<TMessage> messageType, Object message,
            Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
        SendEndpoint endpoint = getSendEndpoint(destination);
        return endpoint.send(messageType, message, contextCallback, cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> send(String destination, Class<TMessage> messageType, Object message,
            Consumer<SendContext> contextCallback) {
        return send(destination, messageType, message, contextCallback, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> send(String destination, Class<TMessage> messageType, Object message) {
        return send(destination, messageType, message, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> scheduleSend(String destination, TMessage message, Duration delay,
            CancellationToken cancellationToken) {
        SendEndpoint endpoint = getSendEndpoint(destination);
        return endpoint.scheduleSend(message, delay, cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> scheduleSend(String destination, TMessage message, Duration delay) {
        return scheduleSend(destination, message, delay, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> scheduleSend(String destination, TMessage message, Instant scheduledTime,
            CancellationToken cancellationToken) {
        SendEndpoint endpoint = getSendEndpoint(destination);
        return endpoint.scheduleSend(message, scheduledTime, cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> scheduleSend(String destination, TMessage message, Instant scheduledTime) {
        return scheduleSend(destination, message, scheduledTime, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> scheduleSend(String destination, Class<TMessage> messageType, Object message,
            Duration delay, CancellationToken cancellationToken) {
        SendEndpoint endpoint = getSendEndpoint(destination);
        return endpoint.scheduleSend(messageType, message, delay, cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> scheduleSend(String destination, Class<TMessage> messageType, Object message,
            Duration delay) {
        return scheduleSend(destination, messageType, message, delay, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> scheduleSend(String destination, Class<TMessage> messageType, Object message,
            Instant scheduledTime, CancellationToken cancellationToken) {
        SendEndpoint endpoint = getSendEndpoint(destination);
        return endpoint.scheduleSend(messageType, message, scheduledTime, cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> scheduleSend(String destination, Class<TMessage> messageType, Object message,
            Instant scheduledTime) {
        return scheduleSend(destination, messageType, message, scheduledTime, CancellationToken.none);
    }

    public <TMessage> CompletableFuture<Void> forward(String destination, TMessage message, CancellationToken cancellationToken) {
        SendEndpoint endpoint = getSendEndpoint(destination);
        return endpoint.send(message, ctx -> ctx.getHeaders().putAll(headers), cancellationToken);
    }

    public <TMessage> CompletableFuture<Void> forward(String destination, TMessage message) {
        return forward(destination, message, CancellationToken.none);
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