package com.myservicebus.persistence.postgresql;

import com.myservicebus.PublishEndpoint;
import com.myservicebus.ScheduleMessageProvider;
import com.myservicebus.ScheduledWorkSource;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.persistence.OutboxSession;
import java.util.Objects;
import javax.sql.DataSource;

/** Dependency-injection registration for durable PostgreSQL scheduling. */
public final class PostgreSqlScheduling {
    private PostgreSqlScheduling() {
    }

    public static ServiceCollection addMessageScheduler(
            ServiceCollection services,
            DataSource dataSource,
            String serviceName) {
        Objects.requireNonNull(services, "services");
        Objects.requireNonNull(dataSource, "dataSource");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }

        services.remove(ScheduleMessageProvider.class);
        services.remove(ScheduledWorkSource.class);
        services.addSingleton(ScheduledWorkSource.class,
                provider -> () -> new PostgreSqlScheduledWorkSource(dataSource, serviceName));
        services.addScoped(ScheduleMessageProvider.class, provider -> () -> {
            return new PostgreSqlScheduleMessageProvider(
                    dataSource,
                    serviceName,
                    provider.getRequiredService(OutboxSession.class),
                    provider.getRequiredService(PublishEndpoint.class),
                    provider.getRequiredService(SendEndpointProvider.class));
        });
        return services;
    }
}
