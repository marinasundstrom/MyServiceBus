package com.myservicebus.persistence.postgresql;

import com.myservicebus.JobClient;
import com.myservicebus.JobConsumerRegistry;
import com.myservicebus.JobProvider;
import com.myservicebus.JobSource;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.serialization.InboundMessageResolver;
import com.myservicebus.serialization.MessageSerializer;
import java.time.Clock;
import java.util.Objects;
import java.util.function.Consumer;
import javax.sql.DataSource;

public final class PostgreSqlJobs {
    private PostgreSqlJobs() {
    }

    /** Uses PostgreSQL for durable tracked-job storage and embedded execution. */
    public static void addBuiltInProvider(
            ServiceCollection services,
            DataSource dataSource,
            String serviceName) {
        addBuiltInProvider(services, dataSource, serviceName, null);
    }

    /** Uses PostgreSQL for durable tracked-job storage and embedded execution. */
    public static void addBuiltInProvider(
            ServiceCollection services,
            DataSource dataSource,
            String serviceName,
            Consumer<PostgreSqlJobOptions> configure) {
        Objects.requireNonNull(services, "services");
        Objects.requireNonNull(dataSource, "dataSource");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }
        PostgreSqlJobOptions options = new PostgreSqlJobOptions();
        if (configure != null) {
            configure.accept(options);
        }
        options.validate();

        services.remove(JobProvider.class);
        services.remove(JobClient.class);
        services.remove(JobSource.class);
        services.addSingleton(PostgreSqlJobOptions.class, () -> options);
        services.addSingleton(PostgreSqlJobProcessor.class, provider -> () ->
                new PostgreSqlJobProcessor(
                        dataSource,
                        serviceName,
                        provider.getRequiredService(JobConsumerRegistry.class),
                        provider,
                        provider.getRequiredService(InboundMessageResolver.class),
                        options,
                        provider.getService(Clock.class)));
        services.addSingleton(PostgreSqlJobService.class, provider -> () ->
                new PostgreSqlJobService(
                        provider.getRequiredService(PostgreSqlJobProcessor.class),
                        options));
        services.addSingleton(JobProvider.class, provider -> () ->
                new PostgreSqlJobProvider(
                        dataSource,
                        serviceName,
                        provider.getRequiredService(JobConsumerRegistry.class),
                        provider.getRequiredService(MessageSerializer.class),
                        provider.getService(Clock.class)));
        services.addSingleton(JobClient.class, provider -> () ->
                provider.getRequiredService(JobProvider.class));
        services.addSingleton(JobSource.class, provider -> () ->
                provider.getRequiredService(JobProvider.class));
    }
}
