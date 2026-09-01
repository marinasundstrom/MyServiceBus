package com.myservicebus.persistence.postgresql;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.orchestration.SagaConcurrencyKind;
import com.myservicebus.orchestration.SagaCorrelationKind;
import com.myservicebus.orchestration.SagaDurabilityKind;
import com.myservicebus.orchestration.SagaOutboxKind;
import com.myservicebus.orchestration.SagaRepository;
import com.myservicebus.orchestration.SagaRepositoryCapabilities;
import com.myservicebus.orchestration.SagaRepositoryMutation;
import com.myservicebus.orchestration.SagaRepositoryTransaction;
import com.myservicebus.persistence.OutboxSession;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.CompletionStage;
import java.util.function.Function;
import javax.sql.DataSource;

/** Stores saga instances and outgoing messages in one PostgreSQL transaction. */
public final class PostgreSqlSagaRepository<TSaga> implements SagaRepository<TSaga> {
    public static final SagaRepositoryCapabilities CAPABILITIES = new SagaRepositoryCapabilities(
            "postgresql",
            SagaCorrelationKind.IDENTITY,
            SagaConcurrencyKind.PESSIMISTIC,
            SagaDurabilityKind.DURABLE,
            SagaOutboxKind.TRANSACTIONAL,
            true);

    private final DataSource dataSource;
    private final OutboxSession outboxSession;
    private final String serviceName;
    private final String sagaType;
    private final Class<TSaga> instanceType;
    private final ObjectMapper objectMapper;

    public PostgreSqlSagaRepository(
            DataSource dataSource,
            OutboxSession outboxSession,
            String serviceName,
            Class<TSaga> instanceType) {
        this(dataSource, outboxSession, serviceName, instanceType.getName(), instanceType,
                new ObjectMapper().findAndRegisterModules());
    }

    public PostgreSqlSagaRepository(
            DataSource dataSource,
            OutboxSession outboxSession,
            String serviceName,
            String sagaType,
            Class<TSaga> instanceType,
            ObjectMapper objectMapper) {
        this.dataSource = Objects.requireNonNull(dataSource, "dataSource");
        this.outboxSession = Objects.requireNonNull(outboxSession, "outboxSession");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }
        if (sagaType == null || sagaType.isBlank()) {
            throw new IllegalArgumentException("sagaType must not be blank");
        }
        this.serviceName = serviceName;
        this.sagaType = sagaType;
        this.instanceType = Objects.requireNonNull(instanceType, "instanceType");
        this.objectMapper = Objects.requireNonNull(objectMapper, "objectMapper");
    }

    @Override
    public SagaRepositoryCapabilities capabilities() {
        return CAPABILITIES;
    }

    @Override
    public <TResult> CompletionStage<TResult> execute(
            UUID correlationId,
            Function<TSaga, CompletionStage<SagaRepositoryTransaction<TSaga, TResult>>> execute) {
        if (correlationId == null || correlationId.equals(new UUID(0, 0))) {
            return CompletableFuture.failedFuture(
                    new IllegalArgumentException("correlationId must not be empty"));
        }
        Objects.requireNonNull(execute, "execute");

        try (Connection connection = dataSource.getConnection()) {
            boolean previousAutoCommit = connection.getAutoCommit();
            connection.setAutoCommit(false);
            try {
                lock(connection, correlationId);
                TSaga instance = load(connection, correlationId);
                SagaRepositoryTransaction<TSaga, TResult> result;
                try (OutboxSession.Registration ignored = PostgreSqlOutboxSession.useTransaction(
                        outboxSession, connection, serviceName)) {
                    result = execute.apply(instance).toCompletableFuture().join();
                    if (result.mutation() == SagaRepositoryMutation.UPSERT) {
                        upsert(connection, correlationId, result.instance());
                    } else if (result.mutation() == SagaRepositoryMutation.DELETE) {
                        delete(connection, correlationId);
                    }
                }
                connection.commit();
                return CompletableFuture.completedFuture(result.result());
            } catch (Exception failure) {
                connection.rollback();
                Throwable cause = failure instanceof CompletionException && failure.getCause() != null
                        ? failure.getCause()
                        : failure;
                return CompletableFuture.failedFuture(cause);
            } finally {
                connection.setAutoCommit(previousAutoCommit);
            }
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    private void lock(Connection connection, UUID correlationId) throws SQLException {
        try (PreparedStatement statement = connection.prepareStatement(
                "SELECT pg_advisory_xact_lock(hashtextextended(?, 0));")) {
            statement.setString(1, sagaType + ":" + correlationId);
            statement.execute();
        }
    }

    private TSaga load(Connection connection, UUID correlationId) throws Exception {
        try (PreparedStatement statement = connection.prepareStatement("""
                SELECT instance::text
                FROM myservicebus.saga_instance
                WHERE saga_type = ? AND correlation_id = ?
                FOR UPDATE;
                """)) {
            statement.setString(1, sagaType);
            statement.setObject(2, correlationId);
            try (ResultSet result = statement.executeQuery()) {
                return result.next() ? objectMapper.readValue(result.getString(1), instanceType) : null;
            }
        }
    }

    private void upsert(Connection connection, UUID correlationId, TSaga instance) throws Exception {
        try (PreparedStatement statement = connection.prepareStatement("""
                INSERT INTO myservicebus.saga_instance (
                    saga_type, correlation_id, instance, revision, created_at_utc, updated_at_utc)
                VALUES (?, ?, ?::jsonb, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (saga_type, correlation_id) DO UPDATE SET
                    instance = EXCLUDED.instance,
                    revision = myservicebus.saga_instance.revision + 1,
                    updated_at_utc = CURRENT_TIMESTAMP;
                """)) {
            statement.setString(1, sagaType);
            statement.setObject(2, correlationId);
            statement.setString(3, objectMapper.writeValueAsString(instance));
            statement.executeUpdate();
        }
    }

    private void delete(Connection connection, UUID correlationId) throws SQLException {
        try (PreparedStatement statement = connection.prepareStatement("""
                DELETE FROM myservicebus.saga_instance
                WHERE saga_type = ? AND correlation_id = ?;
                """)) {
            statement.setString(1, sagaType);
            statement.setObject(2, correlationId);
            statement.executeUpdate();
        }
    }
}
