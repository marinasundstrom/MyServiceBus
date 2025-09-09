package com.myservicebus.http;

import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendTransport;
import com.myservicebus.TransportFactory;
import com.myservicebus.TransportMessage;
import com.myservicebus.topology.MessageBinding;
import java.net.URI;
import java.net.http.HttpClient;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Function;

public class HttpTransportFactory implements TransportFactory {
    private final ConcurrentHashMap<String, HttpClient> clients = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<URI, HttpSendTransport> transports = new ConcurrentHashMap<>();

    @Override
    public SendTransport getSendTransport(URI address) {
        String key = address.getScheme() + "://" + address.getAuthority();
        HttpClient client = clients.computeIfAbsent(key, k -> HttpClient.newHttpClient());
        return transports.computeIfAbsent(address, uri -> new HttpSendTransport(client, uri));
    }

    @Override
    public ReceiveTransport createReceiveTransport(String address, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered, int prefetchCount,
            Map<String, Object> queueArguments) throws Exception {
        URI uri = URI.create(address);
        return HttpReceiveTransport.create(uri, handler, true, isMessageTypeRegistered);
    }

    @Override
    public String getPublishAddress(String exchange) {
        return exchange;
    }

    @Override
    public String getSendAddress(String queue) {
        return queue;
    }
}
