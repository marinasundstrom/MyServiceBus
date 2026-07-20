package com.myservicebus;

import java.net.URI;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;

public interface TransportFactory extends PublishAddressProvider {
    default TransportCapabilityDescriptor getCapabilities() {
        return TransportCapabilityDescriptors.unknown(getClass().getSimpleName());
    }

    SendTransport getSendTransport(URI address);

    /**
     * Creates a receive transport from profile-neutral endpoint intent.
     * This is the supported extension point for new transport implementations.
     */
    default ReceiveTransport createReceiveTransport(ReceiveEndpointTransportTopology topology,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered) throws Exception {
        return createReceiveTransport(
                topology.name(),
                topology.bindings(),
                handler,
                isMessageTypeRegistered,
                topology.prefetchCount(),
                topology.transportOptions());
    }

    /**
     * @deprecated Override {@link #createReceiveTransport(ReceiveEndpointTransportTopology, Function, Function)}
     *             for new transport implementations.
     */
    @Deprecated(forRemoval = false)
    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered, int prefetchCount,
            Map<String, Object> queueArguments) throws Exception {
        return createReceiveTransport(queueName, bindings, handler, isMessageTypeRegistered, prefetchCount);
    }

    /** @deprecated Use the profile-neutral endpoint topology overload. */
    @Deprecated(forRemoval = false)
    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered, int prefetchCount) throws Exception {
        return createReceiveTransport(queueName, bindings, handler);
    }

    /** @deprecated Use the profile-neutral endpoint topology overload. */
    @Deprecated(forRemoval = false)
    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler, int prefetchCount) throws Exception {
        return createReceiveTransport(queueName, bindings, handler, s -> true, prefetchCount);
    }

    /** @deprecated Use the profile-neutral endpoint topology overload. */
    @Deprecated(forRemoval = false)
    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered) throws Exception {
        return createReceiveTransport(queueName, bindings, handler, isMessageTypeRegistered, 0);
    }

    /** @deprecated Use the profile-neutral endpoint topology overload. */
    @Deprecated(forRemoval = false)
    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler) throws Exception {
        throw new UnsupportedOperationException(
                "Transport factory does not implement a receive transport contract");
    }

    String getPublishAddress(String exchange);

    default String getErrorAddress(String endpointName) {
        return getPublishAddress(endpointName + "_error");
    }

    default String getFaultAddress(String endpointName) {
        return getPublishAddress(endpointName + "_fault");
    }

    String getSendAddress(String queue);
}
