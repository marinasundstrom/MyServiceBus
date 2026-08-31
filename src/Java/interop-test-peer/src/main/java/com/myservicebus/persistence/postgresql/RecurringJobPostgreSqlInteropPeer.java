package com.myservicebus.persistence.postgresql;

import com.myservicebus.FixedIntervalRecurringJobCadence;
import com.myservicebus.RecurringJobDefinition;
import com.myservicebus.RecurringJobIdentity;
import com.myservicebus.SendTransport;
import com.myservicebus.TransportFactory;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import java.net.URI;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import org.postgresql.ds.PGSimpleDataSource;

public final class RecurringJobPostgreSqlInteropPeer {
    private RecurringJobPostgreSqlInteropPeer() {
    }

    public static void run(String[] args) throws Exception {
        if (args.length != 3) {
            throw new IllegalArgumentException(
                    "Expected: <postgres-recurring-create|postgres-recurring-materialize> <service-name> <schedule-id>");
        }

        PGSimpleDataSource dataSource = new PGSimpleDataSource();
        dataSource.setServerNames(new String[] { requiredEnvironment("POSTGRES_HOST") });
        dataSource.setPortNumbers(new int[] { Integer.parseInt(requiredEnvironment("POSTGRES_PORT")) });
        dataSource.setDatabaseName(requiredEnvironment("POSTGRES_DATABASE"));
        dataSource.setUser(requiredEnvironment("POSTGRES_USERNAME"));
        dataSource.setPassword(requiredEnvironment("POSTGRES_PASSWORD"));
        Clock clock = Clock.fixed(Instant.parse(requiredEnvironment("RECURRING_NOW")), ZoneOffset.UTC);

        if ("postgres-recurring-create".equals(args[0])) {
            PostgreSqlRecurringJobProvider provider = new PostgreSqlRecurringJobProvider(
                    dataSource,
                    args[1],
                    new NoOpTransportFactory(),
                    new EnvelopeMessageSerializer(),
                    clock);
            provider.addOrUpdate(
                    new RecurringJobDefinition(
                            new RecurringJobIdentity(args[2], "cross-language"),
                            new FixedIntervalRecurringJobCadence(Duration.ofHours(1))),
                    new CrossLanguageRecurringJob("java"),
                    null,
                    CancellationToken.none()).toCompletableFuture().join();
            System.out.println("CREATED");
        } else if ("postgres-recurring-materialize".equals(args[0])) {
            int count = new PostgreSqlRecurringJobMaterializer(dataSource, args[1], clock)
                    .materializeDue().toCompletableFuture().join();
            System.out.println("MATERIALIZED:" + count);
        } else {
            throw new IllegalArgumentException("Unknown mode: " + args[0]);
        }
        System.out.flush();
    }

    public record CrossLanguageRecurringJob(String origin) {
    }

    private static String requiredEnvironment(String name) {
        String value = System.getenv(name);
        if (value == null || value.isBlank()) {
            throw new IllegalStateException(name + " is required");
        }
        return value;
    }

    private static final class NoOpTransportFactory implements TransportFactory {
        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> {
            };
        }

        @Override
        public String getPublishAddress(String exchange) {
            return "loopback://localhost/" + exchange;
        }

        @Override
        public String getSendAddress(String queue) {
            return "loopback://localhost/" + queue;
        }
    }
}
