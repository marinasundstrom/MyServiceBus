package com.myservicebus;

import com.myservicebus.serialization.InboundMessage;
import com.myservicebus.serialization.InboundMessageResolver;
import com.myservicebus.serialization.DefaultInboundMessageResolver;
import com.myservicebus.serialization.EnvelopeMessageDeserializer;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;

import java.net.URI;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Portable request transport composed from temporary receive endpoints and directed sends.
 */
public final class TransportRequestClientTransport implements RequestClientTransport {
    private final TransportFactory transportFactory;
    private final MessageSerializer serializer;
    private final InboundMessageResolver inboundMessageResolver;

    public TransportRequestClientTransport(TransportFactory transportFactory, MessageSerializer serializer) {
        this(
                transportFactory,
                serializer,
                new DefaultInboundMessageResolver(new EnvelopeMessageDeserializer()));
    }

    public TransportRequestClientTransport(
            TransportFactory transportFactory,
            MessageSerializer serializer,
            InboundMessageResolver inboundMessageResolver) {
        this.transportFactory = transportFactory;
        this.serializer = serializer;
        this.inboundMessageResolver = inboundMessageResolver;
    }

    @Override
    public <TRequest, TResponse> CompletableFuture<TResponse> sendRequest(
            Class<TRequest> requestType,
            SendContext context,
            Class<TResponse> responseType) {
        CompletableFuture<TResponse> wireResponse = new CompletableFuture<>();
        CompletableFuture<TResponse> response = new CompletableFuture<>();
        startRequest(
                requestType,
                context,
                List.of(responseType),
                inbound -> {
                    if (isFault(inbound, requestType)) {
                        wireResponse.completeExceptionally(new RequestFaultException(
                                requestType.getSimpleName(), inbound.getMessage(Fault.class)));
                    } else if (inbound.getMessageTypes().contains(MessageUrn.forClass(responseType))) {
                        wireResponse.complete(inbound.getMessage(responseType));
                    }
                },
                wireResponse,
                response);
        return response;
    }

    @Override
    public <TRequest, T1, T2> CompletableFuture<Response2<T1, T2>> sendRequest(
            Class<TRequest> requestType,
            SendContext context,
            Class<T1> responseType1,
            Class<T2> responseType2) {
        CompletableFuture<Response2<T1, T2>> wireResponse = new CompletableFuture<>();
        CompletableFuture<Response2<T1, T2>> response = new CompletableFuture<>();
        startRequest(
                requestType,
                context,
                List.of(responseType1, responseType2),
                inbound -> {
                    if (isFault(inbound, requestType)) {
                        wireResponse.completeExceptionally(new RequestFaultException(
                                requestType.getSimpleName(), inbound.getMessage(Fault.class)));
                    } else if (inbound.getMessageTypes().contains(MessageUrn.forClass(responseType1))) {
                        wireResponse.complete(Response2.fromT1(inbound.getMessage(responseType1)));
                    } else if (inbound.getMessageTypes().contains(MessageUrn.forClass(responseType2))) {
                        wireResponse.complete(Response2.fromT2(inbound.getMessage(responseType2)));
                    }
                },
                wireResponse,
                response);
        return response;
    }

    private <TRequest, TResponse> void startRequest(
            Class<TRequest> requestType,
            SendContext context,
            List<Class<?>> responseTypes,
            InboundHandler handler,
            CompletableFuture<TResponse> wireResponse,
            CompletableFuture<TResponse> response) {
        ReceiveTransport[] receiveTransport = new ReceiveTransport[1];
        AtomicBoolean cleanupStarted = new AtomicBoolean();
        try {
            String responseEndpoint = "resp-" + UUID.randomUUID().toString().replace("-", "");
            List<MessageBinding> bindings = new ArrayList<>();
            for (Class<?> responseType : responseTypes) {
                MessageBinding binding = new MessageBinding();
                binding.setMessageType(responseType);
                binding.setEntityName(responseEndpoint);
                bindings.add(binding);
            }
            ReceiveEndpointTransportTopology topology = new ReceiveEndpointTransportTopology(
                    responseEndpoint,
                    false,
                    true,
                    0,
                    bindings,
                    null);
            UUID requestId = context.getRequestId() != null ? context.getRequestId() : UUID.randomUUID();
            context.setRequestId(requestId);

            receiveTransport[0] = transportFactory.createReceiveTransport(
                    topology,
                    transportMessage -> handleResponse(transportMessage, requestId, handler, wireResponse),
                    null);
            wireResponse.whenCompleteAsync((value, failure) -> {
                if (!cleanupStarted.compareAndSet(false, true)) {
                    return;
                }
                stopQuietly(receiveTransport[0]);
                if (failure == null) {
                    response.complete(value);
                } else {
                    response.completeExceptionally(unwrap(failure));
                }
            });
            response.whenCompleteAsync((ignored, failure) -> {
                if (wireResponse.isDone() || !cleanupStarted.compareAndSet(false, true)) {
                    return;
                }
                stopQuietly(receiveTransport[0]);
                wireResponse.cancel(false);
            });
            receiveTransport[0].start();

            URI responseAddress = URI.create(transportFactory.getTemporaryEndpointAddress(responseEndpoint));
            context.setResponseAddress(responseAddress);
            context.setFaultAddress(responseAddress);
            URI destination = context.getDestinationAddress() != null
                    ? context.getDestinationAddress()
                    : URI.create(transportFactory.getPublishAddress(requestType));
            context.setDestinationAddress(destination);
            byte[] body = context.serialize(serializer);
            transportFactory.getSendTransport(destination)
                    .send(body, context.getHeaders(), serializer.getContentType());
        } catch (Exception exception) {
            wireResponse.completeExceptionally(exception);
        }
    }

    private CompletableFuture<Void> handleResponse(
            TransportMessage transportMessage,
            UUID requestId,
            InboundHandler handler,
            CompletableFuture<?> response) {
        try {
            InboundMessage inbound = inboundMessageResolver.resolve(transportMessage);
            if (!requestId.equals(inbound.getRequestId())) {
                return CompletableFuture.completedFuture(null);
            }
            handler.handle(inbound);
            return CompletableFuture.completedFuture(null);
        } catch (Exception exception) {
            response.completeExceptionally(exception);
            return CompletableFuture.failedFuture(exception);
        }
    }

    private static boolean isFault(InboundMessage inbound, Class<?> requestType) {
        return inbound.getMessageTypes().contains(MessageUrn.forFault(requestType));
    }

    private static Throwable unwrap(Throwable failure) {
        return failure instanceof CompletionException && failure.getCause() != null
                ? failure.getCause()
                : failure;
    }

    private static void stopQuietly(ReceiveTransport transport) {
        if (transport == null) {
            return;
        }
        try {
            transport.stop();
        } catch (Exception ignored) {
            // The request result has already completed; shutdown is best effort.
        }
    }

    @FunctionalInterface
    private interface InboundHandler {
        void handle(InboundMessage inbound) throws Exception;
    }
}
