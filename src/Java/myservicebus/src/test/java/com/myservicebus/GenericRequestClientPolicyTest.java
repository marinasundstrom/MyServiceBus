package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.Duration;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.atomic.AtomicBoolean;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationTokenSource;

class GenericRequestClientPolicyTest {
    static class Request {
    }

    @Test
    void timesOutPendingSingleResponseRequests() {
        PendingTransport transport = new PendingTransport();
        RequestClient<Request> client = new GenericRequestClient<>(Request.class, transport, null,
                RequestTimeout.after(Duration.ofMillis(25)));

        CompletionException exception = assertThrows(CompletionException.class,
                () -> client.getResponse(new Request(), String.class).join());

        assertInstanceOf(java.util.concurrent.TimeoutException.class, exception.getCause());
        assertTrue(transport.singleResponse.isCompletedExceptionally());
    }

    @Test
    void timesOutPendingMultipleResponseRequests() {
        PendingTransport transport = new PendingTransport();
        RequestClient<Request> client = new GenericRequestClient<>(Request.class, transport, null,
                RequestTimeout.after(Duration.ofMillis(25)));

        CompletionException exception = assertThrows(CompletionException.class,
                () -> client.getResponse(new Request(), String.class, Integer.class).join());

        assertInstanceOf(java.util.concurrent.TimeoutException.class, exception.getCause());
        assertTrue(transport.multipleResponse.isCompletedExceptionally());
    }

    @Test
    void cancellationCancelsPendingRequestAndAlreadyCancelledTokensPreventSending() {
        PendingTransport transport = new PendingTransport();
        RequestClient<Request> client = new GenericRequestClient<>(Request.class, transport);
        CancellationTokenSource source = new CancellationTokenSource();

        CompletableFuture<String> response = client.getResponse(new Request(), String.class, source.token());
        source.cancel();

        assertTrue(response.isCancelled());
        assertTrue(transport.singleResponse.isCancelled());

        PendingTransport secondTransport = new PendingTransport();
        RequestClient<Request> secondClient = new GenericRequestClient<>(Request.class, secondTransport);
        CompletableFuture<String> alreadyCancelled = secondClient.getResponse(new Request(), String.class,
                source.token());

        assertTrue(alreadyCancelled.isCancelled());
        assertTrue(!secondTransport.sent.get());
    }

    private static final class PendingTransport implements RequestClientTransport {
        private final AtomicBoolean sent = new AtomicBoolean();
        private final CompletableFuture<String> singleResponse = new CompletableFuture<>();
        private final CompletableFuture<Response2<String, Integer>> multipleResponse = new CompletableFuture<>();

        @Override
        public <TRequest, TResponse> CompletableFuture<TResponse> sendRequest(Class<TRequest> requestType,
                SendContext context, Class<TResponse> responseType) {
            sent.set(true);
            @SuppressWarnings("unchecked")
            CompletableFuture<TResponse> response = (CompletableFuture<TResponse>) singleResponse;
            return response;
        }

        @Override
        public <TRequest, T1, T2> CompletableFuture<Response2<T1, T2>> sendRequest(Class<TRequest> requestType,
                SendContext context, Class<T1> responseType1, Class<T2> responseType2) {
            sent.set(true);
            @SuppressWarnings("unchecked")
            CompletableFuture<Response2<T1, T2>> response = (CompletableFuture<Response2<T1, T2>>) (CompletableFuture<?>) multipleResponse;
            return response;
        }
    }
}
