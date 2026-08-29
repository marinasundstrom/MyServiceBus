package com.myservicebus.testapp.outbox;

import TestApp.OutboxShowcaseMessage;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusServices;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.TransportFactory;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.persistence.OutboxDeliveryService;
import com.myservicebus.persistence.OutboxSession;
import com.myservicebus.persistence.postgresql.PostgreSqlOutboxBacklog;
import com.myservicebus.persistence.postgresql.PostgreSqlOutboxDelivery;
import com.myservicebus.persistence.postgresql.PostgreSqlOutboxHealth;
import com.myservicebus.persistence.postgresql.PostgreSqlOutboxSession;
import com.myservicebus.persistence.postgresql.PostgreSqlSchema;
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;
import io.javalin.Javalin;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.Statement;
import java.time.Instant;
import java.util.UUID;
import org.postgresql.ds.PGSimpleDataSource;

public final class Main {
    private static final String SERVICE_NAME = "outbox-showcase-java";

    private Main() {
    }

    public static void main(String[] args) throws Exception {
        PGSimpleDataSource dataSource = createDataSource();
        PostgreSqlSchema.ensureCreated(dataSource);
        ensureApplicationSchema(dataSource);

        String rabbitHost = env("RABBITMQ_HOST", "localhost");
        int rabbitPort = Integer.parseInt(env("RABBITMQ_PORT", "5672"));
        ServiceCollection services = ServiceCollection.create();
        services.from(MessageBusServices.class).addServiceBus(configurator -> {
            configurator.useBusOutbox();
            configurator.addConsumer(
                    OutboxShowcaseConsumer.class,
                    OutboxShowcaseMessage.class,
                    "outbox-showcase-java-consumer",
                    null);
            configurator.using(RabbitMqFactoryConfigurator.class, (context, rabbit) -> {
                rabbit.host(rabbitHost, rabbitPort, credentials -> {
                    credentials.username("guest");
                    credentials.password("guest");
                });
                rabbit.configureEndpoints(context);
            });
        });

        ServiceProvider provider = services.buildServiceProvider();
        MessageBus bus = provider.getRequiredService(MessageBus.class);
        TransportFactory transport = provider.getRequiredService(TransportFactory.class);
        OutboxDeliveryService delivery = PostgreSqlOutboxDelivery.create(
                dataSource,
                transport,
                SERVICE_NAME,
                options -> {
                    options.setOwnerId("java-" + UUID.randomUUID());
                    options.setPollInterval(java.time.Duration.ofMillis(250));
                });
        PostgreSqlOutboxHealth health = new PostgreSqlOutboxHealth(dataSource, SERVICE_NAME);

        bus.start();
        delivery.start();

        int httpPort = Integer.parseInt(env("HTTP_PORT", "5402"));
        Javalin app = Javalin.create().start(httpPort);
        app.post("/publish", context -> {
            OutboxShowcaseMessage message = new OutboxShowcaseMessage(
                    UUID.randomUUID().toString(),
                    "java",
                    Instant.now().toString());
            try (Connection connection = dataSource.getConnection()) {
                connection.setAutoCommit(false);
                try (PreparedStatement statement = connection.prepareStatement(
                        "INSERT INTO outbox_showcase_event (event_id, origin, created_at_utc) VALUES (?, ?, ?::timestamptz)")) {
                    statement.setString(1, message.getEventId());
                    statement.setString(2, message.getOrigin());
                    statement.setString(3, message.getCreatedAtUtc());
                    statement.executeUpdate();
                }

                try (ServiceScope scope = provider.createScope();
                        OutboxSession.Registration ignored = PostgreSqlOutboxSession.useTransaction(
                                scope.getServiceProvider().getRequiredService(OutboxSession.class),
                                connection,
                                SERVICE_NAME)) {
                    scope.getServiceProvider().getRequiredService(PublishEndpoint.class)
                            .publish(message)
                            .join();
                }
                connection.commit();
            }
            context.status(202).json(message);
        });
        app.get("/received", context -> context.json(OutboxShowcaseConsumer.received()));
        app.get("/health/outbox", context -> {
            PostgreSqlOutboxBacklog backlog = health.getBacklog().join();
            context.json(new OutboxHealthView(delivery.getStatus(), backlog));
        });
        app.get("/health/live", context -> context.status(200).result("live"));

        Runtime.getRuntime().addShutdownHook(new Thread(() -> {
            app.stop();
            delivery.close();
            try {
                bus.stop();
            } catch (Exception exception) {
                throw new IllegalStateException("Failed to stop the bus", exception);
            }
        }));
    }

    private static PGSimpleDataSource createDataSource() {
        PGSimpleDataSource dataSource = new PGSimpleDataSource();
        dataSource.setServerNames(new String[] { env("POSTGRES_HOST", "localhost") });
        dataSource.setPortNumbers(new int[] { Integer.parseInt(env("POSTGRES_PORT", "5432")) });
        dataSource.setDatabaseName(env("POSTGRES_DATABASE", "outbox"));
        dataSource.setUser(env("POSTGRES_USER", "postgres"));
        dataSource.setPassword(env("POSTGRES_PASSWORD", "postgres"));
        return dataSource;
    }

    private static void ensureApplicationSchema(PGSimpleDataSource dataSource) throws Exception {
        try (Connection connection = dataSource.getConnection(); Statement statement = connection.createStatement()) {
            statement.execute("""
                    CREATE TABLE IF NOT EXISTS outbox_showcase_event (
                        event_id text PRIMARY KEY,
                        origin text NOT NULL,
                        created_at_utc timestamptz NOT NULL
                    )
                    """);
        }
    }

    private static String env(String name, String fallback) {
        return System.getenv().getOrDefault(name, fallback);
    }

    private record OutboxHealthView(
            com.myservicebus.persistence.OutboxDeliveryStatus delivery,
            PostgreSqlOutboxBacklog backlog) {
    }
}
