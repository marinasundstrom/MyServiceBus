package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;

public class RequestClientFaultTest {

    static class Ping {
        private final String value;

        Ping(String value) {
            this.value = value;
        }

        String getValue() {
            return value;
        }
    }

    static class Pong {
        private final String value;

        Pong(String value) {
            this.value = value;
        }

        String getValue() {
            return value;
        }
    }

    static class FaultingConsumer implements Consumer<Ping> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<Ping> context) {
            return context.respondFault(new RuntimeException("nope"), context.getCancellationToken());
        }
    }

    @Test
    void request_fault_completes_exceptionally() {
        ServiceCollection services = new ServiceCollection();
        TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {
            cfg.addConsumer(FaultingConsumer.class);
        });

        ServiceProvider provider = services.buildServiceProvider();
        try (ServiceScope scope = provider.createScope()) {
            ServiceProvider scoped = scope.getServiceProvider();
            ScopedClientFactory factory = scoped.getService(ScopedClientFactory.class);
            RequestClient<Ping> client = factory.create(Ping.class);

            CompletableFuture<Pong> response = client.getResponse(new Ping("hi"), Pong.class);
            CompletionException ex = assertThrows(CompletionException.class, response::join);
            assertTrue(ex.getCause() instanceof RequestFaultException);
            RequestFaultException rfe = (RequestFaultException) ex.getCause();
            assertEquals("nope", rfe.getFault().getExceptions().get(0).getMessage());
        }
    }
}
