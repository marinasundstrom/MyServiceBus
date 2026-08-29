package com.myservicebus.persistence.postgresql;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.persistence.OutboxMessage;
import com.myservicebus.persistence.OutboxWriter;
import com.myservicebus.tasks.CancellationToken;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.SQLException;
import java.sql.Types;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.Objects;
import java.util.concurrent.CompletableFuture;

public final class PostgreSqlOutboxWriter implements OutboxWriter {
    private static final ObjectMapper MAPPER = new ObjectMapper();
    private final Connection connection;
    private final String serviceName;

    public PostgreSqlOutboxWriter(Connection connection, String serviceName) {
        this.connection = Objects.requireNonNull(connection, "connection");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }
        this.serviceName = serviceName;
    }

    @Override
    public CompletableFuture<Void> add(OutboxMessage message, CancellationToken cancellationToken) {
        Objects.requireNonNull(message, "message");
        Objects.requireNonNull(cancellationToken, "cancellationToken");
        try {
            cancellationToken.throwIfCancelled();
            requireTransaction();
            addInternal(message);
            return CompletableFuture.completedFuture(null);
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    private void addInternal(OutboxMessage message) throws SQLException, JsonProcessingException {
        String sql = """
                INSERT INTO myservicebus.outbox_message (
                    record_id, service_name, message_id, intent, destination_address, message_types, body, content_type, headers,
                    created_at_utc, request_id, correlation_id, conversation_id, initiator_id, response_address,
                    fault_address, state, next_attempt_at_utc)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?::jsonb, ?, ?, ?, ?, ?, ?, ?, 0, ?);
                """;
        try (PreparedStatement statement = connection.prepareStatement(sql)) {
            statement.setObject(1, message.recordId());
            statement.setString(2, serviceName);
            statement.setObject(3, message.messageId());
            statement.setShort(4, (short) message.intent().ordinal());
            statement.setString(5, message.destinationAddress().toString());
            statement.setArray(6, connection.createArrayOf("text", message.messageTypes().toArray()));
            statement.setBytes(7, message.body());
            statement.setString(8, message.contentType());
            statement.setString(9, MAPPER.writeValueAsString(message.headers()));
            OffsetDateTime createdAt = message.createdAtUtc().atOffset(ZoneOffset.UTC);
            statement.setObject(10, createdAt);
            setNullable(statement, 11, message.requestId(), Types.OTHER);
            setNullable(statement, 12, message.correlationId(), Types.OTHER);
            setNullable(statement, 13, message.conversationId(), Types.OTHER);
            setNullable(statement, 14, message.initiatorId(), Types.OTHER);
            setNullable(statement, 15, message.responseAddress(), Types.VARCHAR);
            setNullable(statement, 16, message.faultAddress(), Types.VARCHAR);
            statement.setObject(17, message.availableAtUtc().atOffset(ZoneOffset.UTC));
            statement.executeUpdate();
        }
    }

    private void requireTransaction() throws SQLException {
        if (connection.isClosed() || connection.getAutoCommit()) {
            throw new IllegalStateException("An active caller-owned PostgreSQL transaction is required.");
        }
    }

    private static void setNullable(PreparedStatement statement, int index, Object value, int sqlType)
            throws SQLException {
        if (value == null) {
            statement.setNull(index, sqlType);
        } else {
            statement.setObject(index, value.toString(), sqlType);
        }
    }
}
