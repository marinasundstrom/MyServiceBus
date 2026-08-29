package com.myservicebus.persistence.postgresql;

import com.myservicebus.persistence.InboxAcquisition;
import com.myservicebus.persistence.InboxMessageKey;
import com.myservicebus.persistence.InboxStore;
import com.myservicebus.persistence.InboxTransaction;
import com.myservicebus.persistence.OutboxWriter;
import com.myservicebus.tasks.CancellationToken;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.Objects;
import java.util.concurrent.CompletableFuture;

public final class PostgreSqlInboxStore implements InboxStore {
    private final Connection connection;
    private final PostgreSqlOutboxWriter outbox;

    public PostgreSqlInboxStore(Connection connection, String serviceName) {
        this.connection = Objects.requireNonNull(connection, "connection");
        this.outbox = new PostgreSqlOutboxWriter(connection, serviceName);
    }

    @Override
    public CompletableFuture<InboxTransaction> acquire(InboxMessageKey key, CancellationToken cancellationToken) {
        try {
            cancellationToken.throwIfCancelled();
            requireTransaction();
            String sql = """
                    INSERT INTO myservicebus.inbox_message (consumer_scope, message_id, state)
                    VALUES (?, ?, 0) ON CONFLICT (consumer_scope, message_id) DO NOTHING;
                    """;
            try (PreparedStatement statement = connection.prepareStatement(sql)) {
                statement.setString(1, key.consumerScope());
                statement.setObject(2, key.messageId());
                if (statement.executeUpdate() == 1) {
                    return CompletableFuture.completedFuture(
                            new Transaction(connection, outbox, key, InboxAcquisition.ACQUIRED));
                }
            }

            String stateSql = """
                    SELECT state FROM myservicebus.inbox_message
                    WHERE consumer_scope = ? AND message_id = ?;
                    """;
            try (PreparedStatement statement = connection.prepareStatement(stateSql)) {
                statement.setString(1, key.consumerScope());
                statement.setObject(2, key.messageId());
                try (ResultSet result = statement.executeQuery()) {
                    if (!result.next()) {
                        throw new IllegalStateException("The conflicting inbox record was not found.");
                    }
                    InboxAcquisition acquisition = result.getShort(1) == 1
                            ? InboxAcquisition.COMPLETED
                            : InboxAcquisition.IN_PROGRESS;
                    return CompletableFuture.completedFuture(new Transaction(connection, outbox, key, acquisition));
                }
            }
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    private void requireTransaction() throws SQLException {
        if (connection.isClosed() || connection.getAutoCommit()) {
            throw new IllegalStateException("An active caller-owned PostgreSQL transaction is required.");
        }
    }

    private static final class Transaction implements InboxTransaction {
        private final Connection connection;
        private final OutboxWriter outbox;
        private final InboxMessageKey key;
        private final InboxAcquisition acquisition;

        private Transaction(
                Connection connection,
                OutboxWriter outbox,
                InboxMessageKey key,
                InboxAcquisition acquisition) {
            this.connection = connection;
            this.outbox = outbox;
            this.key = key;
            this.acquisition = acquisition;
        }

        @Override
        public InboxMessageKey getKey() {
            return key;
        }

        @Override
        public InboxAcquisition getAcquisition() {
            return acquisition;
        }

        @Override
        public OutboxWriter getOutbox() {
            return outbox;
        }

        @Override
        public CompletableFuture<Void> complete() {
            if (acquisition != InboxAcquisition.ACQUIRED) {
                return CompletableFuture.failedFuture(
                        new IllegalStateException("Only an acquired inbox message can be completed."));
            }
            try {
                if (connection.isClosed() || connection.getAutoCommit()) {
                    return CompletableFuture.failedFuture(
                            new IllegalStateException("The caller-owned PostgreSQL transaction is no longer active."));
                }
            } catch (SQLException failure) {
                return CompletableFuture.failedFuture(failure);
            }
            String sql = """
                    UPDATE myservicebus.inbox_message
                    SET state = 1, completed_at_utc = CURRENT_TIMESTAMP
                    WHERE consumer_scope = ? AND message_id = ? AND state = 0;
                    """;
            try (PreparedStatement statement = connection.prepareStatement(sql)) {
                statement.setString(1, key.consumerScope());
                statement.setObject(2, key.messageId());
                if (statement.executeUpdate() != 1) {
                    throw new IllegalStateException("The acquired inbox message could not be completed.");
                }
                return CompletableFuture.completedFuture(null);
            } catch (Exception failure) {
                return CompletableFuture.failedFuture(failure);
            }
        }

        @Override
        public void close() {
        }
    }
}
