package com.myservicebus.persistence.postgresql;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.myservicebus.persistence.InboxAcquisition;
import com.myservicebus.persistence.InboxMessageKey;
import com.myservicebus.persistence.InboxTransaction;
import com.myservicebus.persistence.ExponentialOutboxRetryPolicy;
import com.myservicebus.persistence.OutboxDispatchBatchResult;
import com.myservicebus.persistence.OutboxDispatcher;
import com.myservicebus.persistence.OutboxLease;
import com.myservicebus.persistence.OutboxLeaseRequest;
import com.myservicebus.persistence.OutboxMessage;
import com.myservicebus.persistence.OutboxMessageFactory;
import com.myservicebus.persistence.OutboxSession;
import com.myservicebus.persistence.OutboxTransportDispatcher;
import com.myservicebus.ScheduleCancellationResult;
import com.myservicebus.ScheduleMessageProviderDurability;
import com.myservicebus.ScheduledMessageHandle;
import com.myservicebus.ScheduledWorkSource;
import com.myservicebus.ScheduledWorkState;
import com.myservicebus.ScheduledWorkStatus;
import com.myservicebus.MessageScheduler;
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
import java.sql.ResultSet;
import java.sql.Statement;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.time.temporal.ChronoUnit;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import javax.sql.DataSource;
import org.junit.jupiter.api.Test;
import org.postgresql.ds.PGSimpleDataSource;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.utility.DockerImageName;

class PostgreSqlPersistenceTest {
    private static final String SERVICE_NAME = "orders-service";

    @Test
    void versionTwoSchemaMigratesToSchedulingAndCancellation() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            try (Connection connection = dataSource.getConnection();
                    Statement statement = connection.createStatement()) {
                statement.execute("""
                        UPDATE myservicebus.schema_version SET version = 2 WHERE singleton;
                        ALTER TABLE myservicebus.outbox_message
                            DROP COLUMN scheduled_at_utc,
                            DROP COLUMN cancelled_at_utc,
                            DROP CONSTRAINT outbox_message_state_check;
                        ALTER TABLE myservicebus.outbox_message
                            ADD CONSTRAINT outbox_message_state_check CHECK (state BETWEEN 0 AND 3);
                        """);
            }

            PostgreSqlSchema.ensureCreated(dataSource);

            try (Connection connection = dataSource.getConnection();
                    Statement statement = connection.createStatement();
                    ResultSet result = statement.executeQuery("""
                            SELECT version,
                                EXISTS (
                                    SELECT 1 FROM information_schema.columns
                                    WHERE table_schema = 'myservicebus' AND table_name = 'outbox_message'
                                      AND column_name = 'scheduled_at_utc'),
                                EXISTS (
                                    SELECT 1 FROM information_schema.columns
                                    WHERE table_schema = 'myservicebus' AND table_name = 'outbox_message'
                                      AND column_name = 'cancelled_at_utc')
                            FROM myservicebus.schema_version WHERE singleton;
                            """)) {
                assertTrue(result.next());
                assertEquals(3, result.getInt(1));
                assertTrue(result.getBoolean(2));
                assertTrue(result.getBoolean(3));
            }
        }
    }

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
    void durableSchedulerPersistsIdentityAndCanCancelAfterCommit() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            ServiceCollection services = ServiceCollection.create();
            services.addSingleton(TransportFactory.class, ignored -> () -> new NoOpTransportFactory());
            PostgreSqlScheduling.addMessageScheduler(services, dataSource, SERVICE_NAME);
            services.from(MessageBusServices.class).addServiceBus(configurator -> {
                configurator.useBusOutbox();
                MediatorTransport.configure(configurator);
            });
            ServiceProvider provider = services.buildServiceProvider();
            MessageBus bus = provider.getRequiredService(MessageBus.class);
            bus.start();
            Instant dueAt = Instant.now().plus(Duration.ofMinutes(5));

            try {
                try (ServiceScope scope = provider.createScope();
                        Connection connection = dataSource.getConnection()) {
                    connection.setAutoCommit(false);
                    ServiceProvider scoped = scope.getServiceProvider();
                    MessageScheduler scheduler = scoped.getRequiredService(MessageScheduler.class);
                    assertEquals(ScheduleMessageProviderDurability.DURABLE, scheduler.getDurability());

                    ScheduledMessageHandle handle;
                    try (OutboxSession.Registration ignored = PostgreSqlOutboxSession.useTransaction(
                            scoped.getRequiredService(OutboxSession.class), connection, SERVICE_NAME)) {
                        handle = scheduler.schedulePublish(
                                dueAt,
                                new OrderSubmitted(UUID.randomUUID())).toCompletableFuture().join();
                    }
                    connection.commit();

                    // A newly constructed source models the state a fresh process restores after restart.
                    ScheduledWorkSource source = new PostgreSqlScheduledWorkSource(dataSource, SERVICE_NAME);
                    ScheduledWorkState pending = source.getSnapshot(100).toCompletableFuture().join().stream()
                            .filter(item -> item.tokenId().equals(handle.getTokenId()))
                            .findFirst().orElseThrow();
                    assertEquals(ScheduledWorkStatus.PENDING, pending.status());
                    assertTrue(pending.updatedAtUtc().isBefore(pending.dueAtUtc()));

                    assertEquals(
                            ScheduleCancellationResult.CANCELLED,
                            scheduler.cancelScheduledPublish(handle).toCompletableFuture().join());
                    assertEquals(
                            ScheduleCancellationResult.ALREADY_CANCELLED,
                            scheduler.cancelScheduledPublish(handle).toCompletableFuture().join());
                    ScheduledWorkState cancelled = source.getSnapshot(100).toCompletableFuture().join().stream()
                            .filter(item -> item.tokenId().equals(handle.getTokenId()))
                            .findFirst().orElseThrow();
                    assertEquals("PostgreSQL", cancelled.provider());
                    assertEquals(ScheduleMessageProviderDurability.DURABLE, cancelled.durability());
                    assertEquals(ScheduledWorkStatus.CANCELLED, cancelled.status());
                    assertEquals("Cancelled", cancelled.providerStatus());
                    assertEquals(dueAt, cancelled.dueAtUtc());
                }
            } finally {
                bus.stop();
            }

            List<OutboxLease> leases = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME)
                    .lease(requestAt("replica-a", 10, dueAt.plus(Duration.ofMinutes(1)))).join();
            assertTrue(leases.isEmpty());
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
    void scheduledOutboxMessageIsNotLeasedBeforeItsDueTime() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            Instant now = Instant.now().truncatedTo(ChronoUnit.MILLIS);
            Instant dueAt = now.plus(Duration.ofMinutes(5));
            OutboxMessage message = createMessage(dueAt);
            insertCommitted(dataSource, message);
            PostgreSqlOutboxStore store = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME);

            List<OutboxLease> early = store.lease(requestAt("replica-a", 10, dueAt.minusMillis(1))).join();
            List<OutboxLease> due = store.lease(requestAt("replica-b", 10, dueAt)).join();

            assertTrue(early.isEmpty());
            assertEquals(1, due.size());
            assertEquals(message.recordId(), due.get(0).message().recordId());
            assertEquals(message.messageId(), due.get(0).message().messageId());
            assertEquals(dueAt, due.get(0).message().availableAtUtc());
            assertEquals(dueAt, due.get(0).message().scheduledAtUtc());
        }
    }

    @Test
    void pendingScheduleCanBeCancelledIdempotently() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            Instant dueAt = Instant.now().plus(Duration.ofMinutes(5));
            OutboxMessage message = createMessage(dueAt);
            insertCommitted(dataSource, message);
            PostgreSqlOutboxStore store = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME);

            ScheduleCancellationResult cancelled = store
                    .cancelScheduled(message.messageId(), Instant.now()).join();
            ScheduleCancellationResult repeated = store
                    .cancelScheduled(message.messageId(), Instant.now()).join();
            List<OutboxLease> leases = store.lease(requestAt("replica-a", 10, dueAt)).join();
            PostgreSqlOutboxBacklog backlog = new PostgreSqlOutboxHealth(dataSource, SERVICE_NAME)
                    .getBacklog().join();

            assertEquals(ScheduleCancellationResult.CANCELLED, cancelled);
            assertEquals(ScheduleCancellationResult.ALREADY_CANCELLED, repeated);
            assertTrue(leases.isEmpty());
            assertEquals(1, backlog.cancelled());
        }
    }

    @Test
    void leaseAndCancellationRaceHasOneWinner() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            Instant dueAt = Instant.now();
            OutboxMessage message = createMessage(dueAt);
            insertCommitted(dataSource, message);
            PostgreSqlOutboxStore store = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME);

            CompletableFuture<List<OutboxLease>> leaseFuture = CompletableFuture.supplyAsync(
                    () -> store.lease(requestAt("replica-a", 1, dueAt)).join());
            CompletableFuture<ScheduleCancellationResult> cancellationFuture = CompletableFuture.supplyAsync(
                    () -> store.cancelScheduled(message.messageId(), dueAt).join());
            CompletableFuture.allOf(leaseFuture, cancellationFuture).join();

            boolean leaseWon = leaseFuture.join().size() == 1;
            boolean cancellationWon = cancellationFuture.join() == ScheduleCancellationResult.CANCELLED;
            assertNotEquals(leaseWon, cancellationWon);
            assertEquals(
                    leaseWon
                            ? ScheduleCancellationResult.TOO_LATE
                            : ScheduleCancellationResult.CANCELLED,
                    cancellationFuture.join());
        }
    }

    @Test
    void cancellationDistinguishesUnknownAndNonScheduledMessages() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            OutboxMessage message = createMessage();
            insertCommitted(dataSource, message);
            PostgreSqlOutboxStore store = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME);

            ScheduleCancellationResult notScheduled = store
                    .cancelScheduled(message.messageId(), Instant.now()).join();
            ScheduleCancellationResult notFound = store
                    .cancelScheduled(UUID.randomUUID(), Instant.now()).join();

            assertEquals(ScheduleCancellationResult.NOT_SCHEDULED, notScheduled);
            assertEquals(ScheduleCancellationResult.NOT_FOUND, notFound);
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
    void composedDeliveryServiceDispatchesItsServicePartition() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            OutboxMessage expected = createMessage();
            insertCommitted(dataSource, expected);
            CapturingTransportFactory transport = new CapturingTransportFactory();

            try (com.myservicebus.persistence.OutboxDeliveryService delivery =
                    PostgreSqlOutboxDelivery.create(dataSource, transport, SERVICE_NAME, options -> {
                        options.setOwnerId("orders-replica-a");
                        options.setPollInterval(Duration.ofMillis(10));
                    })) {
                delivery.start();
                assertTrue(transport.sent.await(5, TimeUnit.SECONDS));
            }

            assertArrayEquals(expected.body(), transport.body);
        }
    }

    @Test
    void healthReportsBacklogOnlyForItsServicePartition() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            OutboxMessage ordersMessage = createMessage();
            insertCommitted(dataSource, ordersMessage, SERVICE_NAME);
            insertCommitted(dataSource, createMessage(), "billing-service");

            PostgreSqlOutboxBacklog backlog = new PostgreSqlOutboxHealth(dataSource, SERVICE_NAME)
                    .getBacklog().join();

            assertEquals(SERVICE_NAME, backlog.serviceName());
            assertEquals(1, backlog.pending());
            assertEquals(0, backlog.leased());
            assertEquals(0, backlog.retrying());
            assertEquals(0, backlog.dispatched());
            assertEquals(0, backlog.dead());
            assertEquals(0, backlog.cancelled());
            Duration persistedTimestampDifference = Duration.between(
                            ordersMessage.createdAtUtc(), backlog.oldestUndispatchedAtUtc())
                    .abs();
            assertTrue(
                    persistedTimestampDifference.compareTo(Duration.ofNanos(1_000)) <= 0,
                    () -> "PostgreSQL timestamp differed by " + persistedTimestampDifference);
        }
    }

    @Test
    void failedDispatchRemainsRecoverableWithTheOriginalIdentity() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            OutboxMessage message = createMessage();
            insertCommitted(dataSource, message);
            Instant now = Instant.now();
            MutableClock clock = new MutableClock(now);
            RecordingOutboxTransport transport = new RecordingOutboxTransport(true);
            OutboxDispatcher dispatcher = new OutboxDispatcher(
                    new PostgreSqlOutboxStore(dataSource, SERVICE_NAME),
                    transport,
                    new ExponentialOutboxRetryPolicy(Duration.ofSeconds(1), Duration.ofMinutes(1)),
                    clock);

            OutboxDispatchBatchResult failed = dispatcher
                    .dispatchBatch(requestAt("replica-a", 10, now), CancellationToken.none()).join();
            clock.setInstant(now.plusSeconds(1));
            OutboxDispatchBatchResult recovered = dispatcher
                    .dispatchBatch(requestAt("replica-b", 10, clock.instant()), CancellationToken.none()).join();

            assertEquals(new OutboxDispatchBatchResult(1, 0, 1, 0), failed);
            assertEquals(new OutboxDispatchBatchResult(1, 1, 0, 0), recovered);
            assertEquals(List.of(message.messageId(), message.messageId()), transport.messageIds);
        }
    }

    @Test
    void acceptedButUnmarkedDeliveryIsReclaimedWithTheOriginalIdentity() throws Exception {
        try (PostgreSQLContainer container = startContainer()) {
            DataSource dataSource = dataSource(container);
            PostgreSqlSchema.ensureCreated(dataSource);
            OutboxMessage message = createMessage();
            insertCommitted(dataSource, message);
            Instant now = Instant.now();
            PostgreSqlOutboxStore store = new PostgreSqlOutboxStore(dataSource, SERVICE_NAME);
            OutboxLease firstLease = store
                    .lease(new OutboxLeaseRequest("replica-a", 1, now, Duration.ofSeconds(1))).join().get(0);
            RecordingOutboxTransport transport = new RecordingOutboxTransport(false);

            // Simulate broker acceptance followed by process exit before markDispatched.
            transport.dispatch(firstLease.message(), CancellationToken.none()).join();

            Instant recoveredAt = now.plusSeconds(2);
            OutboxDispatcher dispatcher = new OutboxDispatcher(
                    store,
                    transport,
                    new ExponentialOutboxRetryPolicy(Duration.ofSeconds(1), Duration.ofMinutes(1)),
                    new MutableClock(recoveredAt));
            OutboxDispatchBatchResult recovered = dispatcher
                    .dispatchBatch(requestAt("replica-b", 1, recoveredAt), CancellationToken.none()).join();

            assertEquals(new OutboxDispatchBatchResult(1, 1, 0, 0), recovered);
            assertEquals(List.of(message.messageId(), message.messageId()), transport.messageIds);
        }
    }

    @Test
    void inboxDeduplicatesCompletedIdentityAndCommitsOutboxAtomically() throws Exception {
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

                InboxTransaction distinct = new PostgreSqlInboxStore(connection, SERVICE_NAME)
                        .acquire(new InboxMessageKey(key.consumerScope(), UUID.randomUUID()), CancellationToken.none())
                        .join();
                assertEquals(InboxAcquisition.ACQUIRED, distinct.getAcquisition());
                distinct.complete().join();
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

    private static OutboxLeaseRequest requestAt(String ownerId, int count, Instant now) {
        return new OutboxLeaseRequest(ownerId, count, now, Duration.ofMinutes(1));
    }

    private static OutboxMessage createMessage() {
        return createMessage(null);
    }

    private static OutboxMessage createMessage(Instant scheduledAt) {
        SendContext context = new SendContext(new OrderSubmitted(UUID.randomUUID()));
        context.setMessageId(UUID.randomUUID());
        context.setCorrelationId(UUID.randomUUID());
        context.setDestinationAddress(URI.create("rabbitmq://localhost/exchange/orders"));
        context.setIntent(MessageIntent.PUBLISH);
        context.setScheduledEnqueueTime(scheduledAt);
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

    private static final class CapturingTransportFactory implements TransportFactory {
        private final CountDownLatch sent = new CountDownLatch(1);
        private byte[] body;

        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> {
                body = data.clone();
                sent.countDown();
            };
        }

        @Override
        public String getPublishAddress(String exchange) {
            return "exchange:" + exchange;
        }

        @Override
        public String getSendAddress(String queue) {
            return "queue:" + queue;
        }
    }

    private static final class RecordingOutboxTransport implements OutboxTransportDispatcher {
        private final List<UUID> messageIds = new ArrayList<>();
        private boolean shouldFail;

        private RecordingOutboxTransport(boolean failFirst) {
            shouldFail = failFirst;
        }

        @Override
        public CompletableFuture<Void> dispatch(OutboxMessage message, CancellationToken cancellationToken) {
            messageIds.add(message.messageId());
            if (shouldFail) {
                shouldFail = false;
                return CompletableFuture.failedFuture(new IllegalStateException("broker unavailable"));
            }
            return CompletableFuture.completedFuture(null);
        }
    }

    private static final class MutableClock extends Clock {
        private Instant instant;

        private MutableClock(Instant instant) {
            this.instant = instant;
        }

        private void setInstant(Instant instant) {
            this.instant = instant;
        }

        @Override
        public ZoneId getZone() {
            return ZoneId.of("UTC");
        }

        @Override
        public Clock withZone(ZoneId zone) {
            return this;
        }

        @Override
        public Instant instant() {
            return instant;
        }
    }

    private record OrderSubmitted(UUID orderId) {
    }

    private record SubmitOrder(UUID orderId) {
    }
}
