package com.myservicebus.persistence.postgresql;

import com.myservicebus.JobClient;
import com.myservicebus.JobConsumer;
import com.myservicebus.JobContext;
import com.myservicebus.MessageBusServices;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import org.postgresql.ds.PGSimpleDataSource;

public final class TrackedJobPostgreSqlInteropPeer {
    private static volatile String lastOrigin;

    private TrackedJobPostgreSqlInteropPeer() {
    }

    public static void run(String[] args) {
        if (args.length != 3) {
            throw new IllegalArgumentException(
                    "Expected: <postgres-job-submit|postgres-job-process> <service-name> <origin>");
        }

        PGSimpleDataSource dataSource = new PGSimpleDataSource();
        dataSource.setServerNames(new String[] { requiredEnvironment("POSTGRES_HOST") });
        dataSource.setPortNumbers(new int[] { Integer.parseInt(requiredEnvironment("POSTGRES_PORT")) });
        dataSource.setDatabaseName(requiredEnvironment("POSTGRES_DATABASE"));
        dataSource.setUser(requiredEnvironment("POSTGRES_USERNAME"));
        dataSource.setPassword(requiredEnvironment("POSTGRES_PASSWORD"));

        ServiceCollection registrations = ServiceCollection.create();
        registrations.from(MessageBusServices.class).addServiceBus(configurator ->
                configurator.addJobConsumer(
                        CrossLanguageTrackedJobConsumer.class,
                        CrossLanguageTrackedJob.class,
                        options -> options.setJobTypeName("cross-language-job")));
        PostgreSqlJobs.addBuiltInProvider(registrations, dataSource, args[1]);
        ServiceProvider services = registrations.buildServiceProvider();

        if ("postgres-job-submit".equals(args[0])) {
            services.getRequiredService(JobClient.class)
                    .submit(new CrossLanguageTrackedJob(args[2]))
                    .toCompletableFuture().join();
            System.out.println("SUBMITTED");
        } else if ("postgres-job-process".equals(args[0])) {
            int count = services.getRequiredService(PostgreSqlJobProcessor.class)
                    .processDue().toCompletableFuture().join();
            System.out.println("PROCESSED:" + count + ":" + lastOrigin);
        } else {
            throw new IllegalArgumentException("Unknown mode: " + args[0]);
        }
        System.out.flush();
    }

    public record CrossLanguageTrackedJob(String origin) {
    }

    public static final class CrossLanguageTrackedJobConsumer implements JobConsumer<CrossLanguageTrackedJob> {
        @Override
        public CompletionStage<Void> run(JobContext<CrossLanguageTrackedJob> context) {
            lastOrigin = context.getJob().origin();
            return CompletableFuture.completedFuture(null);
        }
    }

    private static String requiredEnvironment(String name) {
        String value = System.getenv(name);
        if (value == null || value.isBlank()) {
            throw new IllegalStateException(name + " is required");
        }
        return value;
    }
}
