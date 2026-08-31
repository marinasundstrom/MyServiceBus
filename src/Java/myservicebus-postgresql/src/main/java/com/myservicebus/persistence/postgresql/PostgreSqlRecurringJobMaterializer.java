package com.myservicebus.persistence.postgresql;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import com.myservicebus.RecurringJobIdentity;
import com.myservicebus.RecurringJobMisfirePolicy;
import com.myservicebus.RecurringJobNotFoundException;
import com.myservicebus.RecurringJobOccurrenceReceipt;
import com.myservicebus.RecurringJobOccurrenceStatus;
import com.myservicebus.tasks.CancellationToken;
import java.math.BigInteger;
import java.nio.charset.StandardCharsets;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Types;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import javax.sql.DataSource;

public final class PostgreSqlRecurringJobMaterializer {
    private record DueDefinition(
            UUID definitionId, long revision, Instant acceptedAtUtc, Instant startAtUtc, Instant endAtUtc,
            Duration interval, Instant anchorAtUtc, RecurringJobMisfirePolicy misfirePolicy,
            int maxCatchUpOccurrences, String destination, List<String> messageTypes, String envelope,
            String jobTypeName, int retryLimit, Long retryDelayMilliseconds,
            long timeoutMilliseconds, int concurrentJobLimit,
            String contentType, Instant nextDueAtUtc) {
    }

    private static final ObjectMapper MAPPER = new ObjectMapper().findAndRegisterModules();
    private final DataSource dataSource;
    private final String serviceName;
    private final Clock clock;

    public PostgreSqlRecurringJobMaterializer(DataSource dataSource, String serviceName, Clock clock) {
        this.dataSource = Objects.requireNonNull(dataSource, "dataSource");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }
        this.serviceName = serviceName.trim();
        this.clock = clock == null ? Clock.systemUTC() : clock;
    }

    public CompletionStage<Integer> materializeDue(int batchSize, CancellationToken cancellationToken) {
        if (batchSize <= 0) {
            return CompletableFuture.failedFuture(new IllegalArgumentException("batchSize must be positive"));
        }
        try {
            cancellationToken.throwIfCancelled();
            Instant now = clock.instant();
            try (Connection connection = dataSource.getConnection()) {
                connection.setAutoCommit(false);
                try {
                    List<DueDefinition> definitions = readDue(connection, now, batchSize);
                    int materialized = 0;
                    for (DueDefinition definition : definitions) {
                        Evaluation evaluation = evaluate(definition, now);
                        for (Instant scheduledFor : evaluation.occurrences()) {
                            short reason = (short) (evaluation.occurrences().size() > 1
                                    ? 2 : evaluation.misfire() ? 1 : 0);
                            if (materialize(connection, definition, scheduledFor, false, reason, now, null) != null) {
                                materialized++;
                            }
                        }
                        advance(connection, definition.definitionId(), evaluation.next(), now);
                    }
                    connection.commit();
                    return CompletableFuture.completedFuture(materialized);
                } catch (Exception failure) {
                    connection.rollback();
                    throw failure;
                }
            }
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    public CompletionStage<Integer> materializeDue() {
        return materializeDue(32, CancellationToken.none());
    }

    CompletionStage<RecurringJobOccurrenceReceipt> triggerNow(
            RecurringJobIdentity identity, CancellationToken cancellationToken) {
        try {
            cancellationToken.throwIfCancelled();
            Instant now = clock.instant();
            try (Connection connection = dataSource.getConnection()) {
                connection.setAutoCommit(false);
                try {
                    DueDefinition definition = readByIdentity(connection, identity);
                    if (definition == null) {
                        throw new RecurringJobNotFoundException(identity);
                    }
                    UUID occurrenceId = materialize(
                            connection, definition, now, true, (short) 3, now, UUID.randomUUID());
                    if (occurrenceId == null) {
                        throw new IllegalStateException("The manual recurring occurrence could not be materialized");
                    }
                    connection.commit();
                    return CompletableFuture.completedFuture(new RecurringJobOccurrenceReceipt(
                            occurrenceId, definition.definitionId(), definition.revision(), now, true,
                            RecurringJobOccurrenceStatus.PENDING));
                } catch (Exception failure) {
                    connection.rollback();
                    throw failure;
                }
            }
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }

    private List<DueDefinition> readDue(Connection connection, Instant now, int batchSize) throws Exception {
        try (PreparedStatement statement = connection.prepareStatement("""
                SELECT definition_id, revision, accepted_at_utc, start_at_utc, end_at_utc, cadence::text,
                    misfire_policy, max_catch_up_occurrences, destination_address, command_message_types,
                    command_payload::text, job_type_name, job_retry_limit, job_retry_delay_milliseconds,
                    job_timeout_milliseconds, job_concurrent_limit, content_type, next_due_at_utc
                FROM myservicebus.recurring_job_definition
                WHERE service_name = ? AND status = 0 AND next_due_at_utc <= ?
                ORDER BY next_due_at_utc, definition_id LIMIT ? FOR UPDATE SKIP LOCKED
                """)) {
            statement.setString(1, serviceName);
            setInstant(statement, 2, now);
            statement.setInt(3, batchSize);
            return readDefinitions(statement);
        }
    }

    private DueDefinition readByIdentity(Connection connection, RecurringJobIdentity identity) throws Exception {
        try (PreparedStatement statement = connection.prepareStatement("""
                SELECT definition_id, revision, accepted_at_utc, start_at_utc, end_at_utc, cadence::text,
                    misfire_policy, max_catch_up_occurrences, destination_address, command_message_types,
                    command_payload::text, job_type_name, job_retry_limit, job_retry_delay_milliseconds,
                    job_timeout_milliseconds, job_concurrent_limit,
                    content_type, COALESCE(next_due_at_utc, CURRENT_TIMESTAMP)
                FROM myservicebus.recurring_job_definition
                WHERE service_name = ? AND schedule_group = ? AND schedule_id = ? AND status NOT IN (2, 3)
                FOR UPDATE
                """)) {
            statement.setString(1, serviceName);
            statement.setString(2, identity.scheduleGroup() == null ? "" : identity.scheduleGroup());
            statement.setString(3, identity.scheduleId());
            List<DueDefinition> result = readDefinitions(statement);
            return result.isEmpty() ? null : result.get(0);
        }
    }

    private static List<DueDefinition> readDefinitions(PreparedStatement statement) throws Exception {
        List<DueDefinition> definitions = new ArrayList<>();
        try (ResultSet result = statement.executeQuery()) {
            while (result.next()) {
                JsonNode cadence = MAPPER.readTree(result.getString(6));
                BigInteger nanos = new BigInteger(cadence.get("intervalNanoseconds").asText());
                BigInteger[] seconds = nanos.divideAndRemainder(BigInteger.valueOf(1_000_000_000L));
                Duration interval = Duration.ofSeconds(seconds[0].longValueExact(), seconds[1].longValueExact());
                long retryDelay = result.getLong(14);
                Long retryDelayMilliseconds = result.wasNull() ? null : retryDelay;
                definitions.add(new DueDefinition(
                        result.getObject(1, UUID.class), result.getLong(2), instant(result, 3),
                        instant(result, 4), instant(result, 5), interval,
                        cadence.get("anchorAtUtc").isNull() ? null : Instant.parse(cadence.get("anchorAtUtc").asText()),
                        RecurringJobMisfirePolicy.values()[result.getShort(7)], result.getInt(8),
                        result.getString(9), List.of((String[]) result.getArray(10).getArray()), result.getString(11),
                        result.getString(12), result.getInt(13), retryDelayMilliseconds,
                        result.getLong(15), result.getInt(16), result.getString(17), instant(result, 18)));
            }
        }
        return definitions;
    }

    private record Evaluation(List<Instant> occurrences, Instant next, boolean misfire) {
    }

    private static Evaluation evaluate(DueDefinition definition, Instant now) {
        Instant following = calculateNext(definition, definition.nextDueAtUtc());
        if (following == null || following.isAfter(now)) {
            return new Evaluation(List.of(definition.nextDueAtUtc()), following, false);
        }
        List<Instant> occurrences = new ArrayList<>();
        if (definition.misfirePolicy() == RecurringJobMisfirePolicy.FIRE_ONCE_NOW) {
            occurrences.add(definition.nextDueAtUtc());
        } else if (definition.misfirePolicy() == RecurringJobMisfirePolicy.CATCH_UP) {
            for (int index = 0; index < definition.maxCatchUpOccurrences(); index++) {
                Instant value = definition.nextDueAtUtc().plus(definition.interval().multipliedBy(index));
                if (value.isAfter(now) || definition.endAtUtc() != null && !value.isBefore(definition.endAtUtc())) {
                    break;
                }
                occurrences.add(value);
            }
        }
        return new Evaluation(occurrences, calculateNext(definition, now), true);
    }

    private static Instant calculateNext(DueDefinition definition, Instant after) {
        Instant anchor = definition.anchorAtUtc() != null ? definition.anchorAtUtc()
                : definition.startAtUtc() != null ? definition.startAtUtc() : definition.acceptedAtUtc();
        Instant next;
        if (anchor.isAfter(after)) {
            next = anchor;
        } else {
            long steps = Duration.between(anchor, after).dividedBy(definition.interval()) + 1;
            next = anchor.plus(definition.interval().multipliedBy(steps));
        }
        return definition.endAtUtc() != null && !next.isBefore(definition.endAtUtc()) ? null : next;
    }

    private UUID materialize(
            Connection connection, DueDefinition definition, Instant scheduledFor, boolean manual,
            short reason, Instant now, UUID requestedId) throws Exception {
        UUID occurrenceId = requestedId == null ? UUID.randomUUID() : requestedId;
        try (PreparedStatement occurrence = connection.prepareStatement("""
                INSERT INTO myservicebus.recurring_job_occurrence (
                    occurrence_id, definition_id, definition_revision, scheduled_for_utc, materialized_at_utc,
                    materialization_reason, is_manual, status)
                VALUES (?, ?, ?, ?, ?, ?, ?, 0) ON CONFLICT DO NOTHING RETURNING occurrence_id
                """)) {
            occurrence.setObject(1, occurrenceId);
            occurrence.setObject(2, definition.definitionId());
            occurrence.setLong(3, definition.revision());
            setInstant(occurrence, 4, scheduledFor);
            setInstant(occurrence, 5, now);
            occurrence.setShort(6, reason);
            occurrence.setBoolean(7, manual);
            try (ResultSet result = occurrence.executeQuery()) {
                if (!result.next()) {
                    return null;
                }
            }
        }

        UUID jobId = UUID.randomUUID();
        ObjectNode envelope = (ObjectNode) MAPPER.readTree(definition.envelope());
        envelope.put("messageId", jobId.toString());
        envelope.put("conversationId", UUID.randomUUID().toString());
        envelope.put("sentTime", now.toString());
        try (PreparedStatement job = connection.prepareStatement("""
                INSERT INTO myservicebus.job (
                    job_id, service_name, job_type_name, message_types, body, content_type, headers,
                    status, submitted_at_utc, scheduled_for_utc, available_at_utc, updated_at_utc,
                    retry_limit, retry_delay_milliseconds, timeout_milliseconds, concurrent_job_limit,
                    recurring_occurrence_id)
                VALUES (?, ?, ?, ?, ?, ?, '{}'::jsonb, 2, ?, ?, ?, ?, ?, ?, ?, ?, ?);
                UPDATE myservicebus.recurring_job_occurrence SET job_id = ? WHERE occurrence_id = ?
                """)) {
            job.setObject(1, jobId);
            job.setString(2, serviceName);
            job.setString(3, definition.jobTypeName());
            job.setArray(4, connection.createArrayOf("text", definition.messageTypes().toArray()));
            job.setBytes(5, MAPPER.writeValueAsString(envelope).getBytes(StandardCharsets.UTF_8));
            job.setString(6, definition.contentType());
            setInstant(job, 7, now);
            setInstant(job, 8, scheduledFor);
            setInstant(job, 9, now);
            setInstant(job, 10, now);
            job.setInt(11, definition.retryLimit());
            if (definition.retryDelayMilliseconds() == null) {
                job.setNull(12, Types.BIGINT);
            } else {
                job.setLong(12, definition.retryDelayMilliseconds());
            }
            job.setLong(13, definition.timeoutMilliseconds());
            job.setInt(14, definition.concurrentJobLimit());
            job.setObject(15, occurrenceId);
            job.setObject(16, jobId);
            job.setObject(17, occurrenceId);
            job.executeUpdate();
        }
        return occurrenceId;
    }

    private static void advance(Connection connection, UUID definitionId, Instant next, Instant now)
            throws SQLException {
        try (PreparedStatement statement = connection.prepareStatement("""
                UPDATE myservicebus.recurring_job_definition
                SET next_due_at_utc = ?, status = CASE WHEN ? IS NULL THEN 2 ELSE status END,
                    updated_at_utc = ? WHERE definition_id = ?
                """)) {
            setInstant(statement, 1, next);
            setInstant(statement, 2, next);
            setInstant(statement, 3, now);
            statement.setObject(4, definitionId);
            statement.executeUpdate();
        }
    }

    private static Instant instant(ResultSet result, int index) throws SQLException {
        OffsetDateTime value = result.getObject(index, OffsetDateTime.class);
        return value == null ? null : value.toInstant();
    }

    private static void setInstant(PreparedStatement statement, int index, Instant value) throws SQLException {
        if (value == null) {
            statement.setNull(index, Types.TIMESTAMP_WITH_TIMEZONE);
        } else {
            statement.setObject(index, value.atOffset(ZoneOffset.UTC));
        }
    }
}
