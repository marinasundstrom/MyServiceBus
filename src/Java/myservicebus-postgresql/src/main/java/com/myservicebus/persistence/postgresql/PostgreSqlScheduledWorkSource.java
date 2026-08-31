package com.myservicebus.persistence.postgresql;

import com.myservicebus.SchedulingDurability;
import com.myservicebus.ScheduledWorkSource;
import com.myservicebus.ScheduledWorkState;
import com.myservicebus.ScheduledWorkStatus;
import com.myservicebus.persistence.OutboxDeliveryIntent;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.time.OffsetDateTime;
import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import javax.sql.DataSource;

/** Reads scheduled message state from the PostgreSQL outbox without loading message bodies. */
public final class PostgreSqlScheduledWorkSource implements ScheduledWorkSource {
    private final DataSource dataSource;
    private final String serviceName;

    public PostgreSqlScheduledWorkSource(DataSource dataSource, String serviceName) {
        this.dataSource = Objects.requireNonNull(dataSource, "dataSource");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }
        this.serviceName = serviceName;
    }

    @Override
    public String getProvider() {
        return "PostgreSQL";
    }

    @Override
    public boolean isAuthoritative() {
        return true;
    }

    @Override
    public CompletionStage<List<ScheduledWorkState>> getSnapshot(int maximumCount) {
        if (maximumCount <= 0) {
            throw new IllegalArgumentException("maximumCount must be greater than zero");
        }
        try {
            return CompletableFuture.completedFuture(query(maximumCount));
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    private List<ScheduledWorkState> query(int maximumCount) throws Exception {
        String sql = """
                SELECT message_id, intent, destination_address, message_types[1], scheduled_at_utc,
                    state, attempt_count,
                    CASE state
                        WHEN 2 THEN COALESCE(dispatched_at_utc, created_at_utc)
                        WHEN 4 THEN COALESCE(cancelled_at_utc, created_at_utc)
                        ELSE created_at_utc
                    END AS updated_at_utc,
                    failure_category
                FROM myservicebus.outbox_message
                WHERE service_name = ? AND scheduled_at_utc IS NOT NULL
                ORDER BY
                    CASE WHEN state IN (0, 1) THEN 0 ELSE 1 END,
                    CASE WHEN state IN (0, 1) THEN scheduled_at_utc END ASC NULLS LAST,
                    CASE WHEN state NOT IN (0, 1) THEN
                        COALESCE(cancelled_at_utc, dispatched_at_utc, created_at_utc)
                    END DESC NULLS LAST
                LIMIT ?
                """;
        try (Connection connection = dataSource.getConnection();
                PreparedStatement statement = connection.prepareStatement(sql)) {
            statement.setString(1, serviceName);
            statement.setInt(2, maximumCount);
            try (ResultSet result = statement.executeQuery()) {
                List<ScheduledWorkState> items = new ArrayList<>();
                while (result.next()) {
                    short state = result.getShort(6);
                    items.add(new ScheduledWorkState(
                            result.getObject(1, java.util.UUID.class),
                            getProvider(),
                            SchedulingDurability.DURABLE,
                            "Message",
                            result.getString(4),
                            titleCase(OutboxDeliveryIntent.values()[result.getShort(2)].name()),
                            result.getString(3),
                            result.getObject(5, OffsetDateTime.class).toInstant(),
                            mapStatus(state),
                            mapProviderStatus(state),
                            result.getInt(7),
                            result.getObject(8, OffsetDateTime.class).toInstant(),
                            result.getString(9)));
                }
                return List.copyOf(items);
            }
        }
    }

    private static ScheduledWorkStatus mapStatus(short state) {
        return switch (state) {
            case 0 -> ScheduledWorkStatus.PENDING;
            case 1 -> ScheduledWorkStatus.RUNNING;
            case 2 -> ScheduledWorkStatus.COMPLETED;
            case 3 -> ScheduledWorkStatus.FAILED;
            case 4 -> ScheduledWorkStatus.CANCELLED;
            default -> throw new IllegalStateException("Unknown PostgreSQL outbox state '" + state + "'.");
        };
    }

    private static String mapProviderStatus(short state) {
        return switch (state) {
            case 0 -> "Pending";
            case 1 -> "Leased";
            case 2 -> "Dispatched";
            case 3 -> "Dead";
            case 4 -> "Cancelled";
            default -> throw new IllegalStateException("Unknown PostgreSQL outbox state '" + state + "'.");
        };
    }

    private static String titleCase(String value) {
        String lower = value.toLowerCase(java.util.Locale.ROOT);
        return Character.toUpperCase(lower.charAt(0)) + lower.substring(1);
    }
}
