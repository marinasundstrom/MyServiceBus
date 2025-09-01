package com.myservicebus;

import java.util.Map;
import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.NamingConventions;
import com.myservicebus.SendEndpoint;

/**
 * Context passed to consumers when a message is received.
 *
 * <p>
 * Currently the send and publish related members act as no-ops, returning a
 * completed future. This mirrors the .NET implementation where those features
 * are placeholders awaiting a full transport implementation.
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
        SendEndpoint endpoint = getSendEndpoint("rabbitmq://localhost/" + exchange);
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

    @Override
    public CancellationToken getCancellationToken() {
        return cancellationToken;
    }
}