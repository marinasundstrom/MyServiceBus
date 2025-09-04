package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.tasks.CancellationToken;

class InMemoryHarnessDiTest {
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

    static class PingConsumer implements HandlerWithResult<Ping, Pong> {
        @Override
        public CompletableFuture<Pong> handle(Ping message, CancellationToken cancellationToken) {
            return CompletableFuture.completedFuture(new Pong(message.getValue()));
        }
    }

    @Test
    void request_client_round_trip() {
        ServiceCollection services = new ServiceCollection();
        TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {
            cfg.addConsumer(PingConsumer.class);
        });

        ServiceProvider provider = services.buildServiceProvider();
        try (ServiceScope scope = provider.createScope()) {
            ServiceProvider scoped = scope.getServiceProvider();
            RequestClientFactory factory = scoped.getService(RequestClientFactory.class);
            RequestClient<Ping> client = factory.create(Ping.class);
            Pong response = client.getResponse(new Ping("hi"), Pong.class).join();
            assertEquals("hi", response.getValue());
        }
    }
}
