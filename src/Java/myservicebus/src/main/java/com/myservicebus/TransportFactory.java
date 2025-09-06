package com.myservicebus;

import java.net.URI;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import com.myservicebus.topology.MessageBinding;

public interface TransportFactory {
    SendTransport getSendTransport(URI address);

    ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler) throws Exception;

    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler, int prefetchCount) throws Exception {
        return createReceiveTransport(queueName, bindings, handler);
    }

    String getPublishAddress(String exchange);

    String getSendAddress(String queue);
}
