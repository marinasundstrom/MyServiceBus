package com.myservicebus.persistence.postgresql;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.persistence.OutboxDeliveryIntent;
import com.myservicebus.persistence.OutboxLease;
import com.myservicebus.persistence.OutboxLeaseRequest;
import com.myservicebus.persistence.OutboxMessage;
import com.myservicebus.persistence.OutboxStore;
import java.net.URI;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.time.OffsetDateTime;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import javax.sql.DataSource;

public final class PostgreSqlOutboxStore implements OutboxStore {
    private static final ObjectMapper MAPPER = new ObjectMapper();
    private static final TypeReference<Map<String, String>> HEADERS_TYPE = new TypeReference<>() {
    };
    private final DataSource dataSource;
    private final String serviceName;

    public PostgreSqlOutboxStore(DataSource dataSource, String serviceName) {
        this.dataSource = Objects.requireNonNull(dataSource, "dataSource");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }
        this.serviceName = serviceName;
    }

    @Override
    public CompletableFuture<List<OutboxLease>> lease(OutboxLeaseRequest request) {
        try {
            return CompletableFuture.completedFuture(leaseInternal(request));
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    private List<OutboxLease> leaseInternal(OutboxLeaseRequest request) throws Exception {
        String sql = """
                WITH candidates AS (
                    SELECT record_id FROM myservicebus.outbox_message
                    WHERE service_name = ?
                      AND next_attempt_at_utc <= ?
                      AND (state = 0 OR (state = 1 AND lease_expires_at_utc <= ?))
                    ORDER BY created_at_utc, record_id
                    LIMIT ? FOR UPDATE SKIP LOCKED
                )
                UPDATE myservicebus.outbox_message AS message
                SET state = 1, lease_owner = ?, lease_expires_at_utc = ?, attempt_count = attempt_count + 1
                FROM candidates WHERE message.record_id = candidates.record_id
                RETURNING message.record_id, message.message_id, message.intent, message.destination_address,
                    message.message_types, message.body, message.content_type, message.headers::text,
                    message.created_at_utc, message.request_id, message.correlation_id, message.conversation_id,
                    message.initiator_id, message.response_address, message.fault_address,
                    message.lease_expires_at_utc, message.attempt_count - 1;
                """;
        try (Connection connection = dataSource.getConnection()) {
            connection.setAutoCommit(false);
            try (PreparedStatement statement = connection.prepareStatement(sql)) {
                OffsetDateTime now = OffsetDateTime.ofInstant(request.nowUtc(), java.time.ZoneOffset.UTC);
                statement.setString(1, serviceName);
                statement.setObject(2, now);
                statement.setObject(3, now);
                statement.setInt(4, request.maximumCount());
                statement.setString(5, request.ownerId());
                statement.setObject(6, OffsetDateTime.ofInstant(
                        request.nowUtc().plus(request.leaseDuration()), java.time.ZoneOffset.UTC));
                List<OutboxLease> leases = new ArrayList<>();
                try (ResultSet result = statement.executeQuery()) {
                    while (result.next()) {
                        OutboxMessage message = readMessage(result);
                        leases.add(new OutboxLease(
                                message,
                                request.ownerId(),
                                result.getObject(16, OffsetDateTime.class).toInstant(),
                                result.getInt(17)));
                    }
                }
                connection.commit();
                return leases;
            } catch (Exception failure) {
                connection.rollback();
                throw failure;
            }
        }
    }

    @Override
    public CompletableFuture<Boolean> markDispatched(UUID recordId, String ownerId, java.time.Instant dispatchedAtUtc) {
        return ownedUpdate("""
                UPDATE myservicebus.outbox_message
                SET state = 2, dispatched_at_utc = ?, lease_owner = NULL, lease_expires_at_utc = NULL
                WHERE record_id = ? AND service_name = ?
                  AND state = 1 AND lease_owner = ? AND lease_expires_at_utc > ?;
                """, recordId, ownerId, dispatchedAtUtc, null);
    }

    @Override
    public CompletableFuture<Boolean> reschedule(
            UUID recordId,
            String ownerId,
            java.time.Instant nextAttemptAtUtc,
            String failureCategory) {
        return ownedUpdate("""
                UPDATE myservicebus.outbox_message
                SET state = 0, next_attempt_at_utc = ?, failure_category = ?,
                    lease_owner = NULL, lease_expires_at_utc = NULL
                WHERE record_id = ? AND service_name = ? AND state = 1 AND lease_owner = ?;
                """, recordId, ownerId, nextAttemptAtUtc, failureCategory);
    }

    private CompletableFuture<Boolean> ownedUpdate(
            String sql,
            UUID recordId,
            String ownerId,
            java.time.Instant atUtc,
            String failureCategory) {
        try (Connection connection = dataSource.getConnection();
                PreparedStatement statement = connection.prepareStatement(sql)) {
            OffsetDateTime at = OffsetDateTime.ofInstant(atUtc, java.time.ZoneOffset.UTC);
            if (failureCategory == null) {
                statement.setObject(1, at);
                statement.setObject(2, recordId);
                statement.setString(3, serviceName);
                statement.setString(4, ownerId);
                statement.setObject(5, at);
            } else {
                statement.setObject(1, at);
                statement.setString(2, failureCategory);
                statement.setObject(3, recordId);
                statement.setString(4, serviceName);
                statement.setString(5, ownerId);
            }
            return CompletableFuture.completedFuture(statement.executeUpdate() == 1);
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    private static OutboxMessage readMessage(ResultSet result) throws Exception {
        String[] messageTypes = (String[]) result.getArray(5).getArray();
        return new OutboxMessage(
                result.getObject(1, UUID.class),
                result.getObject(2, UUID.class),
                OutboxDeliveryIntent.values()[result.getShort(3)],
                URI.create(result.getString(4)),
                Arrays.asList(messageTypes),
                result.getBytes(6),
                result.getString(7),
                MAPPER.readValue(result.getString(8), HEADERS_TYPE),
                result.getObject(9, OffsetDateTime.class).toInstant(),
                result.getObject(10, UUID.class),
                result.getObject(11, UUID.class),
                result.getObject(12, UUID.class),
                result.getObject(13, UUID.class),
                nullableUri(result, 14),
                nullableUri(result, 15));
    }

    private static URI nullableUri(ResultSet result, int index) throws SQLException {
        String value = result.getString(index);
        return value == null ? null : URI.create(value);
    }
}
