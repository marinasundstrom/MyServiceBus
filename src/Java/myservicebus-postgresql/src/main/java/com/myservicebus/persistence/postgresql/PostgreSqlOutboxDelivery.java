package com.myservicebus.persistence.postgresql;

import com.myservicebus.BusHook;
import com.myservicebus.TransportFactory;
import com.myservicebus.persistence.ExponentialOutboxRetryPolicy;
import com.myservicebus.persistence.OutboxDeliveryOptions;
import com.myservicebus.persistence.OutboxDeliveryService;
import com.myservicebus.persistence.OutboxDispatcher;
import com.myservicebus.persistence.TransportOutboxDispatcher;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import java.util.function.Consumer;
import javax.sql.DataSource;

public final class PostgreSqlOutboxDelivery {
    private PostgreSqlOutboxDelivery() {
    }

    /**
     * Composes the PostgreSQL store, transport dispatcher, retry policy, and explicit delivery lifecycle for one
     * logical service partition. The caller owns {@link OutboxDeliveryService#start()} and
     * {@link OutboxDeliveryService#close()}.
     */
    public static OutboxDeliveryService create(
            DataSource dataSource,
            TransportFactory transportFactory,
            String serviceName,
            Consumer<OutboxDeliveryOptions> configure) {
        return create(dataSource, transportFactory, serviceName, List.of(), configure);
    }

    public static OutboxDeliveryService create(
            DataSource dataSource,
            TransportFactory transportFactory,
            String serviceName,
            Iterable<? extends BusHook> hooks,
            Consumer<OutboxDeliveryOptions> configure) {
        Objects.requireNonNull(dataSource, "dataSource");
        Objects.requireNonNull(transportFactory, "transportFactory");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }

        OutboxDeliveryOptions options = new OutboxDeliveryOptions();
        options.setServiceName(serviceName);
        if (configure != null) {
            configure.accept(options);
        }
        List<BusHook> hookList = new ArrayList<>();
        hooks.forEach(hookList::add);
        OutboxDispatcher dispatcher = new OutboxDispatcher(
                new PostgreSqlOutboxStore(dataSource, serviceName),
                new TransportOutboxDispatcher(transportFactory, hookList),
                new ExponentialOutboxRetryPolicy(Duration.ofSeconds(1), Duration.ofMinutes(1)));
        return new OutboxDeliveryService(
                dispatcher,
                options,
                new PostgreSqlOutboxHealth(dataSource, serviceName),
                hookList);
    }

    public static OutboxDeliveryService create(
            DataSource dataSource,
            TransportFactory transportFactory,
            String serviceName) {
        return create(dataSource, transportFactory, serviceName, null);
    }
}
