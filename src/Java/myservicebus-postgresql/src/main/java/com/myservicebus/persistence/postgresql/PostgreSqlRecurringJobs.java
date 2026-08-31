package com.myservicebus.persistence.postgresql;

import com.myservicebus.RecurringJobProvider;
import com.myservicebus.RecurringJobSource;
import com.myservicebus.JobConsumerRegistry;
import com.myservicebus.TransportFactory;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.serialization.MessageSerializer;
import java.time.Clock;
import java.util.Objects;
import javax.sql.DataSource;

public final class PostgreSqlRecurringJobs {
    private PostgreSqlRecurringJobs() {
    }

    /** Uses PostgreSQL storage for the built-in durable recurring-job provider. */
    public static void addBuiltInProvider(
            ServiceCollection services,
            DataSource dataSource,
            String serviceName) {
        Objects.requireNonNull(services, "services");
        Objects.requireNonNull(dataSource, "dataSource");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }

        services.remove(RecurringJobProvider.class);
        services.remove(RecurringJobSource.class);
        services.addSingleton(PostgreSqlRecurringJobMaterializer.class, provider -> () ->
                new PostgreSqlRecurringJobMaterializer(
                        dataSource,
                        serviceName,
                        provider.getService(Clock.class)));
        services.addSingleton(PostgreSqlRecurringJobService.class, provider -> () ->
                new PostgreSqlRecurringJobService(
                        provider.getRequiredService(PostgreSqlRecurringJobMaterializer.class)));
        services.addSingleton(RecurringJobProvider.class, provider -> () ->
                new PostgreSqlRecurringJobProvider(
                        dataSource,
                        serviceName,
                        provider.getRequiredService(TransportFactory.class),
                        provider.getRequiredService(MessageSerializer.class),
                        provider.getService(Clock.class),
                        provider.getRequiredService(PostgreSqlRecurringJobMaterializer.class),
                        provider.getRequiredService(JobConsumerRegistry.class)));
        services.addSingleton(RecurringJobSource.class, provider -> () ->
                (RecurringJobSource) provider.getRequiredService(RecurringJobProvider.class));
    }
}
