package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationToken;

class RequestClientHeaderTest {
    static class MyRequest { }

    @Test
    void request_client_applies_headers() throws Exception {
        AtomicReference<Object> header = new AtomicReference<>();

        RequestClientTransport transport = new RequestClientTransport() {
            @Override
            public <TRequest, TResponse> CompletableFuture<TResponse> sendRequest(Class<TRequest> requestType, SendContext context, Class<TResponse> responseType) {
                header.set(context.getHeaders().get("trace-id"));
                return CompletableFuture.completedFuture(null);
            }

            @Override
            public <TRequest, T1, T2> CompletableFuture<Response2<T1, T2>> sendRequest(Class<TRequest> requestType, SendContext context, Class<T1> responseType1, Class<T2> responseType2) {
                return CompletableFuture.completedFuture(null);
            }
        };

        RequestClient<MyRequest> client = new GenericRequestClient<>(MyRequest.class, transport);
        client.getResponse(new MyRequest(), Void.class, c -> c.getHeaders().put("trace-id", "abc"), CancellationToken.none).join();

        assertEquals("abc", header.get());
    }
}
