package com.myservicebus.persistence.postgresql;

import com.myservicebus.persistence.OutboxBacklogProvider;
import com.myservicebus.persistence.OutboxBacklogSnapshot;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.time.OffsetDateTime;
import java.util.Objects;
import java.util.concurrent.CompletableFuture;
import javax.sql.DataSource;

public final class PostgreSqlOutboxHealth implements OutboxBacklogProvider {
    private final DataSource dataSource;
    private final String serviceName;

    public PostgreSqlOutboxHealth(DataSource dataSource, String serviceName) {
        this.dataSource = Objects.requireNonNull(dataSource, "dataSource");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }
        this.serviceName = serviceName;
    }

    public CompletableFuture<PostgreSqlOutboxBacklog> getBacklog() {
        String sql = """
                SELECT
                    count(*) FILTER (WHERE state = 0 AND attempt_count = 0),
                    count(*) FILTER (WHERE state = 1),
                    count(*) FILTER (WHERE state = 0 AND attempt_count > 0),
                    count(*) FILTER (WHERE state = 2),
                    count(*) FILTER (WHERE state = 3),
                    count(*) FILTER (WHERE state = 4),
                    min(created_at_utc) FILTER (WHERE state IN (0, 1))
                FROM myservicebus.outbox_message
                WHERE service_name = ?;
                """;
        try (Connection connection = dataSource.getConnection();
                PreparedStatement statement = connection.prepareStatement(sql)) {
            statement.setString(1, serviceName);
            try (ResultSet result = statement.executeQuery()) {
                if (!result.next()) {
                    return CompletableFuture.failedFuture(
                            new IllegalStateException("PostgreSQL did not return an outbox backlog snapshot."));
                }
                OffsetDateTime oldest = result.getObject(7, OffsetDateTime.class);
                return CompletableFuture.completedFuture(new PostgreSqlOutboxBacklog(
                        serviceName,
                        result.getInt(1),
                        result.getInt(2),
                        result.getInt(3),
                        result.getInt(4),
                        result.getInt(5),
                        result.getInt(6),
                        oldest == null ? null : oldest.toInstant()));
            }
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    @Override
    public CompletableFuture<OutboxBacklogSnapshot> getSnapshot() {
        return getBacklog().thenApply(backlog -> new OutboxBacklogSnapshot(
                backlog.pending(),
                backlog.leased(),
                backlog.retrying(),
                backlog.dispatched(),
                backlog.dead(),
                backlog.cancelled(),
                backlog.oldestUndispatchedAtUtc()));
    }
}
