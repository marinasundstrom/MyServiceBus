package com.myservicebus;

import java.net.URI;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import com.myservicebus.topology.MessageBinding;

public interface TransportFactory {
    SendTransport getSendTransport(URI address);

    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered, int prefetchCount,
            Map<String, Object> queueArguments) throws Exception {
        return createReceiveTransport(queueName, bindings, handler, isMessageTypeRegistered, prefetchCount);
    }

    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered, int prefetchCount) throws Exception {
        return createReceiveTransport(queueName, bindings, handler);
    }

    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler, int prefetchCount) throws Exception {
        return createReceiveTransport(queueName, bindings, handler, s -> true, prefetchCount);
    }

    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered) throws Exception {
        return createReceiveTransport(queueName, bindings, handler, isMessageTypeRegistered, 0);
    }

    default ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler) throws Exception {
        return createReceiveTransport(queueName, bindings, handler, s -> true, 0);
    }

    String getPublishAddress(String exchange);

    String getSendAddress(String queue);
}
