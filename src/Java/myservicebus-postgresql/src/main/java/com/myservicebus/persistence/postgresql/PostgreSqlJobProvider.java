package com.myservicebus.persistence.postgresql;

import com.myservicebus.JobAttemptState;
import com.myservicebus.JobAttemptStatus;
import com.myservicebus.JobClient;
import com.myservicebus.JobConsumerRegistry;
import com.myservicebus.JobControlOutcome;
import com.myservicebus.JobControlResult;
import com.myservicebus.JobProgress;
import com.myservicebus.JobProvider;
import com.myservicebus.JobSource;
import com.myservicebus.JobState;
import com.myservicebus.JobStatus;
import com.myservicebus.JobSubmissionOptions;
import com.myservicebus.JobSubmissionReceipt;
import com.myservicebus.MessageUrn;
import com.myservicebus.SchedulingDurability;
import com.myservicebus.SchedulingPlacement;
import com.myservicebus.SendContext;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import java.net.URI;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Types;
import java.time.Clock;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import javax.sql.DataSource;

final class PostgreSqlJobProvider implements JobProvider, JobSource {
    private final DataSource dataSource;
    private final String serviceName;
    private final JobConsumerRegistry consumers;
    private final MessageSerializer serializer;
    private final Clock clock;

    PostgreSqlJobProvider(
            DataSource dataSource,
            String serviceName,
            JobConsumerRegistry consumers,
            MessageSerializer serializer,
            Clock clock) {
        this.dataSource = Objects.requireNonNull(dataSource, "dataSource");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }
        this.serviceName = serviceName.trim();
        this.consumers = Objects.requireNonNull(consumers, "consumers");
        this.serializer = Objects.requireNonNull(serializer, "serializer");
        this.clock = clock == null ? Clock.systemUTC() : clock;
    }

    @Override
    public String getProviderName() {
        return "MyServiceBus.Durable";
    }

    @Override
    public SchedulingDurability getDurability() {
        return SchedulingDurability.DURABLE;
    }

    @Override
    public SchedulingPlacement getPlacement() {
        return SchedulingPlacement.EMBEDDED;
    }

    @Override
    public boolean isAuthoritative() {
        return true;
    }

    @Override
    public <TJob> CompletionStage<JobSubmissionReceipt> submit(
            TJob job,
            JobSubmissionOptions options,
            CancellationToken cancellationToken) {
        return store(job, options, null, cancellationToken);
    }

    @Override
    public <TJob> CompletionStage<JobSubmissionReceipt> schedule(
            Instant startAtUtc,
            TJob job,
            JobSubmissionOptions options,
            CancellationToken cancellationToken) {
        return store(job, options, Objects.requireNonNull(startAtUtc, "startAtUtc"), cancellationToken);
    }

    @Override
    public CompletionStage<JobControlResult> cancel(UUID jobId, CancellationToken cancellationToken) {
        try {
            cancellationToken.throwIfCancelled();
            try (Connection connection = dataSource.getConnection()) {
                connection.setAutoCommit(false);
                JobStatus status = readStatus(connection, jobId, true);
                if (status == null) {
                    connection.rollback();
                    return CompletableFuture.completedFuture(new JobControlResult(JobControlOutcome.NOT_FOUND, null));
                }
                if (status == JobStatus.COMPLETED || status == JobStatus.FAULTED || status == JobStatus.CANCELLED) {
                    connection.rollback();
                    return CompletableFuture.completedFuture(new JobControlResult(JobControlOutcome.UNCHANGED, status));
                }

                Instant now = clock.instant();
                JobStatus next = status == JobStatus.SUBMITTED
                                || status == JobStatus.SCHEDULED
                                || status == JobStatus.WAITING
                        ? JobStatus.CANCELLED
                        : status;
                try (PreparedStatement command = connection.prepareStatement("""
                        UPDATE myservicebus.job
                        SET cancellation_requested_at_utc = ?, status = ?,
                            completed_at_utc = CASE WHEN ? = 6 THEN ? ELSE completed_at_utc END,
                            updated_at_utc = ?
                        WHERE service_name = ? AND job_id = ?
                        """)) {
                    setInstant(command, 1, now);
                    command.setShort(2, (short) next.ordinal());
                    command.setShort(3, (short) next.ordinal());
                    setInstant(command, 4, now);
                    setInstant(command, 5, now);
                    command.setString(6, serviceName);
                    command.setObject(7, jobId);
                    command.executeUpdate();
                }
                if (next == JobStatus.CANCELLED) {
                    try (PreparedStatement occurrence = connection.prepareStatement("""
                            UPDATE myservicebus.recurring_job_occurrence occurrence
                            SET status = 6
                            FROM myservicebus.job job
                            WHERE job.recurring_occurrence_id = occurrence.occurrence_id
                              AND job.service_name = ? AND job.job_id = ?
                            """)) {
                        occurrence.setString(1, serviceName);
                        occurrence.setObject(2, jobId);
                        occurrence.executeUpdate();
                    }
                }
                connection.commit();
                return CompletableFuture.completedFuture(new JobControlResult(JobControlOutcome.APPLIED, next));
            }
        } catch (Exception exception) {
            return CompletableFuture.failedFuture(exception);
        }
    }

    @Override
    public CompletionStage<JobControlResult> retry(UUID jobId, CancellationToken cancellationToken) {
        try {
            cancellationToken.throwIfCancelled();
            Instant now = clock.instant();
            try (Connection connection = dataSource.getConnection()) {
                connection.setAutoCommit(false);
                try (PreparedStatement command = connection.prepareStatement("""
                            UPDATE myservicebus.job
                            SET status = 2, available_at_utc = ?, completed_at_utc = NULL,
                                cancellation_requested_at_utc = NULL, lease_owner = NULL,
                                lease_expires_at_utc = NULL, failure_type = NULL,
                                failure_message = NULL, updated_at_utc = ?
                            WHERE service_name = ? AND job_id = ? AND status IN (5, 6)
                            """)) {
                    setInstant(command, 1, now);
                    setInstant(command, 2, now);
                    command.setString(3, serviceName);
                    command.setObject(4, jobId);
                    if (command.executeUpdate() == 1) {
                        try (PreparedStatement occurrence = connection.prepareStatement("""
                                UPDATE myservicebus.recurring_job_occurrence occurrence
                                SET status = 4, failure_category = NULL
                                FROM myservicebus.job job
                                WHERE job.recurring_occurrence_id = occurrence.occurrence_id
                                  AND job.service_name = ? AND job.job_id = ?
                                """)) {
                            occurrence.setString(1, serviceName);
                            occurrence.setObject(2, jobId);
                            occurrence.executeUpdate();
                        }
                        connection.commit();
                        return CompletableFuture.completedFuture(
                                new JobControlResult(JobControlOutcome.APPLIED, JobStatus.WAITING));
                    }
                }
                connection.rollback();
            }

            JobStatus current = readStatus(jobId);
            return CompletableFuture.completedFuture(current == null
                    ? new JobControlResult(JobControlOutcome.NOT_FOUND, null)
                    : new JobControlResult(JobControlOutcome.INVALID_STATE, current));
        } catch (Exception exception) {
            return CompletableFuture.failedFuture(exception);
        }
    }

    @Override
    public CompletionStage<List<JobState>> getSnapshot(int maximumCount, CancellationToken cancellationToken) {
        if (maximumCount <= 0) {
            throw new IllegalArgumentException("maximumCount must be greater than zero");
        }
        try {
            cancellationToken.throwIfCancelled();
            List<JobState> jobs = new ArrayList<>();
            try (Connection connection = dataSource.getConnection();
                    PreparedStatement command = connection.prepareStatement("""
                            SELECT job_id, job_type_name, status, submitted_at_utc, scheduled_for_utc,
                                started_at_utc, completed_at_utc, progress_value, progress_limit,
                                recurring_occurrence_id, updated_at_utc
                            FROM myservicebus.job
                            WHERE service_name = ?
                            ORDER BY updated_at_utc DESC, job_id
                            LIMIT ?
                            """)) {
                command.setString(1, serviceName);
                command.setInt(2, maximumCount);
                try (ResultSet rows = command.executeQuery()) {
                    while (rows.next()) {
                        Long progressValue = nullableLong(rows, 8);
                        Long progressLimit = nullableLong(rows, 9);
                        jobs.add(new JobState(
                                rows.getObject(1, UUID.class),
                                rows.getString(2),
                                JobStatus.values()[rows.getShort(3)],
                                getProviderName(),
                                getDurability(),
                                getPlacement(),
                                instant(rows, 4),
                                nullableInstant(rows, 5),
                                nullableInstant(rows, 6),
                                nullableInstant(rows, 7),
                                progressValue == null ? null : new JobProgress(progressValue, progressLimit),
                                rows.getObject(10, UUID.class),
                                instant(rows, 11)));
                    }
                }
            }
            return CompletableFuture.completedFuture(List.copyOf(jobs));
        } catch (Exception exception) {
            return CompletableFuture.failedFuture(exception);
        }
    }

    @Override
    public CompletionStage<List<JobAttemptState>> getAttempts(
            UUID jobId,
            int maximumCount,
            CancellationToken cancellationToken) {
        if (maximumCount <= 0) {
            throw new IllegalArgumentException("maximumCount must be greater than zero");
        }
        try {
            cancellationToken.throwIfCancelled();
            List<JobAttemptState> attempts = new ArrayList<>();
            try (Connection connection = dataSource.getConnection();
                    PreparedStatement command = connection.prepareStatement("""
                            SELECT attempt_id, job_id, retry_attempt, status, started_at_utc,
                                completed_at_utc, fault_type, fault_message
                            FROM myservicebus.job_attempt
                            WHERE job_id = ? AND EXISTS (
                                SELECT 1 FROM myservicebus.job
                                WHERE job.job_id = job_attempt.job_id AND service_name = ?)
                            ORDER BY retry_attempt DESC
                            LIMIT ?
                            """)) {
                command.setObject(1, jobId);
                command.setString(2, serviceName);
                command.setInt(3, maximumCount);
                try (ResultSet rows = command.executeQuery()) {
                    while (rows.next()) {
                        attempts.add(new JobAttemptState(
                                rows.getObject(1, UUID.class),
                                rows.getObject(2, UUID.class),
                                rows.getInt(3),
                                JobAttemptStatus.values()[rows.getShort(4)],
                                instant(rows, 5),
                                nullableInstant(rows, 6),
                                rows.getString(7),
                                rows.getString(8)));
                    }
                }
            }
            Collections.reverse(attempts);
            return CompletableFuture.completedFuture(List.copyOf(attempts));
        } catch (Exception exception) {
            return CompletableFuture.failedFuture(exception);
        }
    }

    private <TJob> CompletionStage<JobSubmissionReceipt> store(
            TJob job,
            JobSubmissionOptions options,
            Instant scheduledForUtc,
            CancellationToken cancellationToken) {
        try {
            Objects.requireNonNull(job, "job");
            cancellationToken.throwIfCancelled();
            JobConsumerRegistry.Descriptor descriptor = consumers.get(job.getClass());
            Instant now = clock.instant();
            if (scheduledForUtc != null && scheduledForUtc.isBefore(now)) {
                scheduledForUtc = now;
            }
            UUID jobId = options != null && options.jobId() != null ? options.jobId() : UUID.randomUUID();
            JobStatus status = scheduledForUtc == null ? JobStatus.WAITING : JobStatus.SCHEDULED;
            SendContext context = new SendContext(job, cancellationToken);
            context.setMessageId(jobId);
            context.setDestinationAddress(URI.create("loopback://localhost/jobs/" + descriptor.jobTypeName()));
            List<String> messageTypes = MessageUrn.forMessageTypes(job.getClass());
            context.setMessageTypes(messageTypes);
            byte[] body = context.serialize(serializer);

            try (Connection connection = dataSource.getConnection();
                    PreparedStatement command = connection.prepareStatement("""
                            INSERT INTO myservicebus.job (
                                job_id, service_name, job_type_name, message_types, body, content_type, headers,
                                status, submitted_at_utc, scheduled_for_utc, available_at_utc, updated_at_utc,
                                retry_limit, retry_delay_milliseconds, timeout_milliseconds, concurrent_job_limit,
                                recurring_occurrence_id)
                            VALUES (?, ?, ?, ?, ?, ?, '{}'::jsonb, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                            """)) {
                command.setObject(1, jobId);
                command.setString(2, serviceName);
                command.setString(3, descriptor.jobTypeName());
                command.setArray(4, connection.createArrayOf("text", messageTypes.toArray()));
                command.setBytes(5, body);
                command.setString(6, serializer.getContentType());
                command.setShort(7, (short) status.ordinal());
                setInstant(command, 8, now);
                setNullableInstant(command, 9, scheduledForUtc);
                setInstant(command, 10, scheduledForUtc == null ? now : scheduledForUtc);
                setInstant(command, 11, now);
                command.setInt(12, descriptor.options().getRetryCount());
                if (descriptor.options().getRetryDelay() == null) {
                    command.setNull(13, Types.BIGINT);
                } else {
                    command.setLong(13, descriptor.options().getRetryDelay().toMillis());
                }
                command.setLong(14, descriptor.options().getJobTimeout().toMillis());
                command.setInt(15, descriptor.options().getConcurrentJobLimit());
                if (options == null || options.recurringJobOccurrenceId() == null) {
                    command.setNull(16, Types.OTHER);
                } else {
                    command.setObject(16, options.recurringJobOccurrenceId());
                }
                command.executeUpdate();
            } catch (SQLException exception) {
                if ("23505".equals(exception.getSQLState())) {
                    throw new IllegalStateException("Job '" + jobId + "' already exists.", exception);
                }
                throw exception;
            }
            return CompletableFuture.completedFuture(
                    new JobSubmissionReceipt(jobId, status, now, scheduledForUtc));
        } catch (Exception exception) {
            return CompletableFuture.failedFuture(exception);
        }
    }

    private JobStatus readStatus(UUID jobId) throws SQLException {
        try (Connection connection = dataSource.getConnection()) {
            return readStatus(connection, jobId, false);
        }
    }

    private JobStatus readStatus(Connection connection, UUID jobId, boolean lock) throws SQLException {
        try (PreparedStatement command = connection.prepareStatement(
                "SELECT status FROM myservicebus.job WHERE service_name = ? AND job_id = ?"
                        + (lock ? " FOR UPDATE" : ""))) {
            command.setString(1, serviceName);
            command.setObject(2, jobId);
            try (ResultSet rows = command.executeQuery()) {
                return rows.next() ? JobStatus.values()[rows.getShort(1)] : null;
            }
        }
    }

    static void setInstant(PreparedStatement command, int index, Instant value) throws SQLException {
        command.setObject(index, OffsetDateTime.ofInstant(value, ZoneOffset.UTC));
    }

    static void setNullableInstant(PreparedStatement command, int index, Instant value) throws SQLException {
        if (value == null) {
            command.setNull(index, Types.TIMESTAMP_WITH_TIMEZONE);
        } else {
            setInstant(command, index, value);
        }
    }

    static Instant instant(ResultSet rows, int index) throws SQLException {
        return rows.getObject(index, OffsetDateTime.class).toInstant();
    }

    static Instant nullableInstant(ResultSet rows, int index) throws SQLException {
        OffsetDateTime value = rows.getObject(index, OffsetDateTime.class);
        return value == null ? null : value.toInstant();
    }

    private static Long nullableLong(ResultSet rows, int index) throws SQLException {
        long value = rows.getLong(index);
        return rows.wasNull() ? null : value;
    }
}
