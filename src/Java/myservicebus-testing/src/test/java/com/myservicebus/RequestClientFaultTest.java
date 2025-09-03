package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

class RequestClientFaultTest {

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

        ServiceProvider provider = services.build();
        RequestClientFactory factory = provider.getService(RequestClientFactory.class);
        RequestClient<Ping> client = factory.create(Ping.class);

        CompletableFuture<Pong> response = client.getResponse(new Ping("hi"), Pong.class);
        var ex = assertThrows(java.util.concurrent.CompletionException.class, response::join);
        assertEquals("nope", ex.getCause().getMessage());
    }
}

