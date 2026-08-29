package com.myservicebus.persistence.postgresql;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;

import com.myservicebus.persistence.InboxAcquisition;
import com.myservicebus.persistence.InboxMessageKey;
import com.myservicebus.persistence.InboxTransaction;
import com.myservicebus.persistence.OutboxLease;
import com.myservicebus.persistence.OutboxLeaseRequest;
import com.myservicebus.persistence.OutboxMessage;
import com.myservicebus.persistence.OutboxMessageFactory;
import com.myservicebus.persistence.OutboxSession;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusServices;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.SendContext;
import com.myservicebus.TransportFactory;
import com.myservicebus.SendTransport;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.mediator.MediatorTransport;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.serialization.MessageIntent;
import com.myservicebus.tasks.CancellationToken;
import java.net.URI;
import java.sql.Connection;
import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import javax.sql.DataSource;
import org.junit.jupiter.api.Test;
import org.postgresql.ds.PGSimpleDataSource;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.utility.DockerImageName;

class PostgreSqlPersistenceTest {
    private static final String SERVICE_NAME = "orders-service";

    @Test
    void scopedBusEndpointsCaptureMessagesInApplicationTransaction() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            ServiceCollection services = ServiceCollection.create();
            services.addSingleton(TransportFactory.class, ignored -> () -> new NoOpTransportFactory());
            services.from(MessageBusServices.class).addServiceBus(configurator -> {
                configurator.useBusOutbox();
                MediatorTransport.configure(configurator);
            });
            ServiceProvider provider = services.buildServiceProvider();
            MessageBus bus = provider.getRequiredService(MessageBus.class);
            bus.start();

            try {
                try (ServiceScope scope = provider.createScope();
                        Connection connection = dataSource.getConnection()) {
                    connection.setAutoCommit(false);
                    ServiceProvider scoped = scope.getServiceProvider();
                    try (OutboxSession.Registration ignored = PostgreSqlOutboxSession.useTransaction(
                            scoped.getRequiredService(OutboxSession.class), connection, SERVICE_NAME)) {
                        PublishEndpoint publishEndpoint = scoped.getRequiredService(PublishEndpoint.class);
                        publishEndpoint.publish(new OrderSubmitted(UUID.randomUUID())).join();

                        SendEndpointProvider endpointProvider = scoped.getRequiredService(SendEndpointProvider.class);
                        SendEndpoint endpoint = endpointProvider.getSendEndpoint("loopback://localhost/orders");
                        endpoint.send(new SubmitOrder(UUID.randomUUID())).join();
                    }
                    connection.commit();
                }
            } finally {
                bus.stop();
            }

            List<OutboxLease> leases = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME)
                    .lease(request("replica-a", 10)).join();
            assertEquals(2, leases.size());
            assertEquals(com.myservicebus.persistence.OutboxDeliveryIntent.PUBLISH, leases.get(0).message().intent());
            assertEquals(com.myservicebus.persistence.OutboxDeliveryIntent.SEND, leases.get(1).message().intent());
            assertEquals("loopback://localhost/orders", leases.get(1).message().destinationAddress().toString());
        }
    }

    @Test
    void outboxWriteCommitsAndRollsBackWithApplicationTransaction() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            PostgreSqlSchema.ensureCreated(dataSource);

            OutboxMessage rolledBack = createMessage();
            try (Connection connection = dataSource.getConnection()) {
                connection.setAutoCommit(false);
                new PostgreSqlOutboxWriter(connection, SERVICE_NAME)
                        .add(rolledBack, CancellationToken.none()).join();
                connection.rollback();
            }

            OutboxMessage committed = createMessage();
            insertCommitted(dataSource, committed);
            List<OutboxLease> leases = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME)
                    .lease(request("replica-a", 10)).join();

            assertEquals(1, leases.size());
            assertEquals(committed.recordId(), leases.get(0).message().recordId());
            assertEquals(committed.messageId(), leases.get(0).message().messageId());
            assertEquals(committed.intent(), leases.get(0).message().intent());
            assertEquals(committed.destinationAddress(), leases.get(0).message().destinationAddress());
            assertEquals(committed.messageTypes(), leases.get(0).message().messageTypes());
            assertArrayEquals(committed.body(), leases.get(0).message().body());
            assertEquals(committed.contentType(), leases.get(0).message().contentType());
            assertEquals(committed.headers(), leases.get(0).message().headers());
            assertEquals(committed.correlationId(), leases.get(0).message().correlationId());
        }
    }

    @Test
    void competingDispatchersLeaseDisjointRecords() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            insertCommitted(dataSource, createMessage());
            insertCommitted(dataSource, createMessage());

            PostgreSqlOutboxStore storeA = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME);
            PostgreSqlOutboxStore storeB = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME);
            CompletableFuture<List<OutboxLease>> leasesAFuture = CompletableFuture.supplyAsync(
                    () -> storeA.lease(request("replica-a", 1)).join());
            CompletableFuture<List<OutboxLease>> leasesBFuture = CompletableFuture.supplyAsync(
                    () -> storeB.lease(request("replica-b", 1)).join());
            List<OutboxLease> leasesA = leasesAFuture.join();
            List<OutboxLease> leasesB = leasesBFuture.join();

            assertEquals(1, leasesA.size());
            assertEquals(1, leasesB.size());
            assertNotEquals(leasesA.get(0).message().recordId(), leasesB.get(0).message().recordId());
        }
    }

    @Test
    void logicalServicesLeaseOnlyTheirOwnOutboxPartition() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            OutboxMessage ordersMessage = createMessage();
            OutboxMessage billingMessage = createMessage();
            insertCommitted(dataSource, ordersMessage, "orders-service");
            insertCommitted(dataSource, billingMessage, "billing-service");

            List<OutboxLease> ordersLeases = new PostgreSqlOutboxStore(dataSource, "orders-service")
                    .lease(request("orders-replica-a", 10)).join();
            List<OutboxLease> billingLeases = new PostgreSqlOutboxStore(dataSource, "billing-service")
                    .lease(request("billing-replica-a", 10)).join();

            assertEquals(ordersMessage.recordId(), ordersLeases.get(0).message().recordId());
            assertEquals(1, ordersLeases.size());
            assertEquals(billingMessage.recordId(), billingLeases.get(0).message().recordId());
            assertEquals(1, billingLeases.size());
        }
    }

    @Test
    void inboxCompletionAndOutboxWriteCommitAtomically() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            InboxMessageKey key = new InboxMessageKey("billing-charge-card", UUID.randomUUID());
            OutboxMessage message = createMessage();

            try (Connection connection = dataSource.getConnection()) {
                connection.setAutoCommit(false);
                InboxTransaction acquisition = new PostgreSqlInboxStore(connection, SERVICE_NAME)
                        .acquire(key, CancellationToken.none()).join();
                assertEquals(InboxAcquisition.ACQUIRED, acquisition.getAcquisition());
                acquisition.getOutbox().add(message, CancellationToken.none()).join();
                acquisition.complete().join();
                connection.commit();
            }

            try (Connection connection = dataSource.getConnection()) {
                connection.setAutoCommit(false);
                InboxTransaction duplicate = new PostgreSqlInboxStore(connection, SERVICE_NAME)
                        .acquire(key, CancellationToken.none()).join();
                assertEquals(InboxAcquisition.COMPLETED, duplicate.getAcquisition());
                connection.commit();
            }

            List<OutboxLease> leases = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME)
                    .lease(request("replica-a", 10)).join();
            assertEquals(1, leases.size());
            assertEquals(message.messageId(), leases.get(0).message().messageId());
        }
    }

    private static PostgreSQLContainer startContainer() {
        PostgreSQLContainer container = new PostgreSQLContainer(
                DockerImageName.parse("postgres:17.6-alpine"));
        container.start();
        return container;
    }

    private static DataSource dataSource(PostgreSQLContainer container) {
        PGSimpleDataSource dataSource = new PGSimpleDataSource();
        dataSource.setURL(container.getJdbcUrl());
        dataSource.setUser(container.getUsername());
        dataSource.setPassword(container.getPassword());
        return dataSource;
    }

    private static void insertCommitted(DataSource dataSource, OutboxMessage message) throws Exception {
        insertCommitted(dataSource, message, SERVICE_NAME);
    }

    private static void insertCommitted(DataSource dataSource, OutboxMessage message, String serviceName)
            throws Exception {
        try (Connection connection = dataSource.getConnection()) {
            connection.setAutoCommit(false);
            new PostgreSqlOutboxWriter(connection, serviceName).add(message, CancellationToken.none()).join();
            connection.commit();
        }
    }

    private static OutboxLeaseRequest request(String ownerId, int count) {
        return new OutboxLeaseRequest(ownerId, count, Instant.now(), Duration.ofMinutes(1));
    }

    private static OutboxMessage createMessage() {
        SendContext context = new SendContext(new OrderSubmitted(UUID.randomUUID()));
        context.setMessageId(UUID.randomUUID());
        context.setCorrelationId(UUID.randomUUID());
        context.setDestinationAddress(URI.create("rabbitmq://localhost/exchange/orders"));
        context.setIntent(MessageIntent.PUBLISH);
        context.getHeaders().put("traceparent", "00-test");
        try {
            return OutboxMessageFactory.create(context, new EnvelopeMessageSerializer());
        } catch (Exception failure) {
            throw new IllegalStateException("Could not create the persisted test envelope.", failure);
        }
    }

    private static final class NoOpTransportFactory implements TransportFactory {
        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> {
            };
        }

        @Override
        public String getPublishAddress(String exchange) {
            return "loopback://" + exchange;
        }

        @Override
        public String getSendAddress(String queue) {
            return "loopback://" + queue;
        }
    }

    private record OrderSubmitted(UUID orderId) {
    }

    private record SubmitOrder(UUID orderId) {
    }
}
