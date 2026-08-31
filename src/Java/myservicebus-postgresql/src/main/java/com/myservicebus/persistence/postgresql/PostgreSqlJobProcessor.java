package com.myservicebus.persistence.postgresql;

import com.myservicebus.JobAttemptStatus;
import com.myservicebus.JobConsumerRegistry;
import com.myservicebus.JobExecutionContext;
import com.myservicebus.JobProgress;
import com.myservicebus.JobStatus;
import com.myservicebus.TransportMessage;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.serialization.InboundMessage;
import com.myservicebus.serialization.InboundMessageResolver;
import com.myservicebus.serialization.MassTransitHeaderConvention;
import com.myservicebus.tasks.CancellationRegistration;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.tasks.CancellationTokenSource;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Types;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CancellationException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.CompletionStage;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import javax.sql.DataSource;

public final class PostgreSqlJobProcessor {
    private record Lease(
            UUID jobId,
            UUID attemptId,
            int retryAttempt,
            String jobTypeName,
            byte[] body,
            String contentType,
            int retryLimit,
            Long retryDelayMilliseconds,
            long timeoutMilliseconds,
            Instant startedAtUtc,
            UUID recurringOccurrenceId) {
    }

    private final DataSource dataSource;
    private final String serviceName;
    private final String workerId;
    private final JobConsumerRegistry consumers;
    private final ServiceProvider services;
    private final InboundMessageResolver inboundMessageResolver;
    private final PostgreSqlJobOptions options;
    private final Clock clock;

    public PostgreSqlJobProcessor(
            DataSource dataSource,
            String serviceName,
            JobConsumerRegistry consumers,
            ServiceProvider services,
            InboundMessageResolver inboundMessageResolver,
            PostgreSqlJobOptions options,
            Clock clock) {
        this.dataSource = Objects.requireNonNull(dataSource, "dataSource");
        if (serviceName == null || serviceName.isBlank()) {
            throw new IllegalArgumentException("serviceName must not be blank");
        }
        this.serviceName = serviceName.trim();
        this.consumers = Objects.requireNonNull(consumers, "consumers");
        this.services = Objects.requireNonNull(services, "services");
        this.inboundMessageResolver = Objects.requireNonNull(inboundMessageResolver, "inboundMessageResolver");
        this.options = Objects.requireNonNull(options, "options");
        options.validate();
        this.clock = clock == null ? Clock.systemUTC() : clock;
        workerId = System.getProperty("user.name", "worker") + ":" + ProcessHandle.current().pid() + ":" + UUID.randomUUID();
    }

    public CompletionStage<Integer> processDue() {
        return processDue(options.getBatchSize(), CancellationToken.none());
    }

    public CompletionStage<Integer> processDue(int maximumCount, CancellationToken cancellationToken) {
        if (maximumCount <= 0) {
            throw new IllegalArgumentException("maximumCount must be greater than zero");
        }
        try {
            cancellationToken.throwIfCancelled();
            faultExhaustedLeases();
            List<Lease> leases = new ArrayList<>();
            for (int index = 0; index < maximumCount; index++) {
                Lease lease = tryLease();
                if (lease == null) {
                    break;
                }
                leases.add(lease);
            }
            CompletableFuture<?>[] work = leases.stream()
                    .map(lease -> CompletableFuture.runAsync(() -> execute(lease, cancellationToken)))
                    .toArray(CompletableFuture[]::new);
            CompletableFuture.allOf(work).join();
            return CompletableFuture.completedFuture(leases.size());
        } catch (Exception exception) {
            return CompletableFuture.failedFuture(exception);
        }
    }

    private Lease tryLease() throws SQLException {
        Instant now = clock.instant();
        try (Connection connection = dataSource.getConnection()) {
            connection.setAutoCommit(false);
            Lease lease;
            try (PreparedStatement command = connection.prepareStatement("""
                    WITH candidate AS (
                        SELECT job_id FROM myservicebus.job
                        WHERE service_name = ? AND cancellation_requested_at_utc IS NULL
                          AND ((status IN (1, 2) AND available_at_utc <= ?)
                            OR (status = 3 AND lease_expires_at_utc <= ? AND attempt_count <= retry_limit))
                        ORDER BY available_at_utc, submitted_at_utc, job_id
                        FOR UPDATE SKIP LOCKED LIMIT 1
                    )
                    UPDATE myservicebus.job job
                    SET status = 3, started_at_utc = COALESCE(job.started_at_utc, ?),
                        updated_at_utc = ?, lease_owner = ?, lease_expires_at_utc = ?,
                        attempt_count = job.attempt_count + 1
                    FROM candidate WHERE job.job_id = candidate.job_id
                    RETURNING job.job_id, job.attempt_count - 1, job.job_type_name, job.body,
                        job.content_type, job.retry_limit, job.retry_delay_milliseconds,
                        job.timeout_milliseconds, job.recurring_occurrence_id
                    """)) {
                command.setString(1, serviceName);
                PostgreSqlJobProvider.setInstant(command, 2, now);
                PostgreSqlJobProvider.setInstant(command, 3, now);
                PostgreSqlJobProvider.setInstant(command, 4, now);
                PostgreSqlJobProvider.setInstant(command, 5, now);
                command.setString(6, workerId);
                PostgreSqlJobProvider.setInstant(command, 7, now.plus(options.getLeaseDuration()));
                try (ResultSet rows = command.executeQuery()) {
                    if (!rows.next()) {
                        connection.commit();
                        return null;
                    }
                    long retryDelay = rows.getLong(7);
                    Long retryDelayMilliseconds = rows.wasNull() ? null : retryDelay;
                    lease = new Lease(
                            rows.getObject(1, UUID.class),
                            UUID.randomUUID(),
                            rows.getInt(2),
                            rows.getString(3),
                            rows.getBytes(4),
                            rows.getString(5),
                            rows.getInt(6),
                            retryDelayMilliseconds,
                            rows.getLong(8),
                            now,
                            rows.getObject(9, UUID.class));
                }
            }

            if (lease.recurringOccurrenceId() != null) {
                try (PreparedStatement occurrence = connection.prepareStatement("""
                        UPDATE myservicebus.recurring_job_occurrence
                        SET status = 3 WHERE occurrence_id = ?
                        """)) {
                    occurrence.setObject(1, lease.recurringOccurrenceId());
                    occurrence.executeUpdate();
                }
            }

            try (PreparedStatement stale = connection.prepareStatement("""
                    UPDATE myservicebus.job_attempt
                    SET status = 2, completed_at_utc = ?,
                        fault_type = 'MyServiceBus.JobLeaseExpired',
                        fault_message = 'The worker lease expired before the attempt completed.'
                    WHERE job_id = ? AND status = 0
                    """)) {
                PostgreSqlJobProvider.setInstant(stale, 1, now);
                stale.setObject(2, lease.jobId());
                stale.executeUpdate();
            }
            try (PreparedStatement attempt = connection.prepareStatement("""
                    INSERT INTO myservicebus.job_attempt (
                        attempt_id, job_id, retry_attempt, status, worker_id, started_at_utc)
                    VALUES (?, ?, ?, 0, ?, ?)
                    """)) {
                attempt.setObject(1, lease.attemptId());
                attempt.setObject(2, lease.jobId());
                attempt.setInt(3, lease.retryAttempt());
                attempt.setString(4, workerId);
                PostgreSqlJobProvider.setInstant(attempt, 5, now);
                attempt.executeUpdate();
            }
            connection.commit();
            return lease;
        }
    }

    private void execute(Lease lease, CancellationToken stoppingToken) {
        JobConsumerRegistry.Descriptor descriptor = consumers.get(lease.jobTypeName());
        boolean acquired = false;
        try {
            while (!acquired) {
                acquired = descriptor.concurrency().tryAcquire(
                        options.getHeartbeatInterval().toMillis(),
                        TimeUnit.MILLISECONDS);
                if (!acquired && renewLease(lease.jobId())) {
                    finish(lease, JobStatus.CANCELLED, JobAttemptStatus.CANCELLED, null, null);
                    return;
                }
                stoppingToken.throwIfCancelled();
            }
            Object job = deserialize(lease, descriptor.jobType());
            CancellationTokenSource cancellation = new CancellationTokenSource();
            try (CancellationRegistration registration = stoppingToken.onCancel(cancellation::cancel);
                    ServiceScope scope = services.createScope()) {
                JobExecutionContext context = new JobExecutionContext(
                        lease.jobId(),
                        lease.attemptId(),
                        lease.retryAttempt(),
                        job,
                        lease.startedAtUtc(),
                        cancellation.token(),
                        progress -> updateProgress(lease.jobId(), progress));
                CompletionStage<Void> stage = descriptor.run(scope.getServiceProvider(), context);
                scope.detach();
                long deadline = System.nanoTime() + TimeUnit.MILLISECONDS.toNanos(lease.timeoutMilliseconds());
                while (true) {
                    long remaining = deadline - System.nanoTime();
                    if (remaining <= 0) {
                        cancellation.cancel();
                        throw new TimeoutException("Job '" + lease.jobId() + "' exceeded its timeout of "
                                + Duration.ofMillis(lease.timeoutMilliseconds()));
                    }
                    long wait = Math.min(
                            TimeUnit.NANOSECONDS.toMillis(remaining),
                            options.getHeartbeatInterval().toMillis());
                    try {
                        stage.toCompletableFuture().get(Math.max(1, wait), TimeUnit.MILLISECONDS);
                        break;
                    } catch (TimeoutException heartbeat) {
                        if (renewLease(lease.jobId())) {
                            cancellation.cancel();
                        }
                    }
                }
                boolean cancelled = isCancellationRequested(lease.jobId());
                finish(
                        lease,
                        cancelled ? JobStatus.CANCELLED : JobStatus.COMPLETED,
                        cancelled ? JobAttemptStatus.CANCELLED : JobAttemptStatus.COMPLETED,
                        null,
                        null);
            }
        } catch (Exception exception) {
            Throwable failure = unwrap(exception);
            if (stoppingToken.isCancelled()) {
                return;
            }
            try {
                if (isCancellationRequested(lease.jobId()) || failure instanceof CancellationException) {
                    finish(lease, JobStatus.CANCELLED, JobAttemptStatus.CANCELLED, null, null);
                } else {
                    boolean retry = lease.retryAttempt() < lease.retryLimit();
                    finish(
                            lease,
                            retry ? JobStatus.WAITING : JobStatus.FAULTED,
                            JobAttemptStatus.FAULTED,
                            failure,
                            retry ? lease.retryDelayMilliseconds() : null);
                }
            } catch (SQLException persistenceFailure) {
                throw new CompletionException(persistenceFailure);
            }
        } finally {
            if (acquired) {
                descriptor.concurrency().release();
            }
        }
    }

    private Object deserialize(Lease lease, Class<?> jobType) throws Exception {
        TransportMessage transport = new TransportMessage(
                lease.body(),
                Map.of(MassTransitHeaderConvention.INSTANCE.getContentTypeHeader(), lease.contentType()));
        InboundMessage inbound = inboundMessageResolver.resolve(transport);
        return inbound.getMessage(jobType);
    }

    private void updateProgress(UUID jobId, JobProgress progress) {
        Instant now = clock.instant();
        try (Connection connection = dataSource.getConnection();
                PreparedStatement command = connection.prepareStatement("""
                        UPDATE myservicebus.job
                        SET progress_value = ?, progress_limit = ?, updated_at_utc = ?
                        WHERE service_name = ? AND job_id = ? AND status = 3 AND lease_owner = ?
                        """)) {
            command.setLong(1, progress.value());
            if (progress.limit() == null) {
                command.setNull(2, Types.BIGINT);
            } else {
                command.setLong(2, progress.limit());
            }
            PostgreSqlJobProvider.setInstant(command, 3, now);
            command.setString(4, serviceName);
            command.setObject(5, jobId);
            command.setString(6, workerId);
            command.executeUpdate();
        } catch (SQLException exception) {
            throw new CompletionException(exception);
        }
    }

    private boolean renewLease(UUID jobId) throws SQLException {
        Instant now = clock.instant();
        try (Connection connection = dataSource.getConnection();
                PreparedStatement command = connection.prepareStatement("""
                        UPDATE myservicebus.job
                        SET lease_expires_at_utc = ?, updated_at_utc = ?
                        WHERE service_name = ? AND job_id = ? AND status = 3 AND lease_owner = ?
                        RETURNING cancellation_requested_at_utc IS NOT NULL
                        """)) {
            PostgreSqlJobProvider.setInstant(command, 1, now.plus(options.getLeaseDuration()));
            PostgreSqlJobProvider.setInstant(command, 2, now);
            command.setString(3, serviceName);
            command.setObject(4, jobId);
            command.setString(5, workerId);
            try (ResultSet rows = command.executeQuery()) {
                return rows.next() && rows.getBoolean(1);
            }
        }
    }

    private boolean isCancellationRequested(UUID jobId) throws SQLException {
        try (Connection connection = dataSource.getConnection();
                PreparedStatement command = connection.prepareStatement("""
                        SELECT cancellation_requested_at_utc IS NOT NULL
                        FROM myservicebus.job WHERE service_name = ? AND job_id = ?
                        """)) {
            command.setString(1, serviceName);
            command.setObject(2, jobId);
            try (ResultSet rows = command.executeQuery()) {
                return rows.next() && rows.getBoolean(1);
            }
        }
    }

    private void finish(
            Lease lease,
            JobStatus jobStatus,
            JobAttemptStatus attemptStatus,
            Throwable failure,
            Long retryDelayMilliseconds) throws SQLException {
        Instant now = clock.instant();
        Instant available = retryDelayMilliseconds == null ? now : now.plusMillis(retryDelayMilliseconds);
        try (Connection connection = dataSource.getConnection()) {
            connection.setAutoCommit(false);
            try (PreparedStatement job = connection.prepareStatement("""
                    UPDATE myservicebus.job
                    SET status = ?, completed_at_utc = CASE WHEN ? IN (4, 5, 6) THEN ? ELSE NULL END,
                        available_at_utc = ?, updated_at_utc = ?, lease_owner = NULL,
                        lease_expires_at_utc = NULL, failure_type = ?, failure_message = ?
                    WHERE service_name = ? AND job_id = ? AND status = 3 AND lease_owner = ?
                    """)) {
                job.setShort(1, (short) jobStatus.ordinal());
                job.setShort(2, (short) jobStatus.ordinal());
                PostgreSqlJobProvider.setInstant(job, 3, now);
                PostgreSqlJobProvider.setInstant(job, 4, available);
                PostgreSqlJobProvider.setInstant(job, 5, now);
                if (failure == null) {
                    job.setNull(6, Types.VARCHAR);
                    job.setNull(7, Types.VARCHAR);
                } else {
                    job.setString(6, failure.getClass().getName());
                    job.setString(7, failure.getMessage());
                }
                job.setString(8, serviceName);
                job.setObject(9, lease.jobId());
                job.setString(10, workerId);
                if (job.executeUpdate() == 0) {
                    connection.rollback();
                    return;
                }
            }
            try (PreparedStatement attempt = connection.prepareStatement("""
                    UPDATE myservicebus.job_attempt
                    SET status = ?, completed_at_utc = ?, fault_type = ?, fault_message = ?
                    WHERE attempt_id = ? AND status = 0
                    """)) {
                attempt.setShort(1, (short) attemptStatus.ordinal());
                PostgreSqlJobProvider.setInstant(attempt, 2, now);
                if (failure == null) {
                    attempt.setNull(3, Types.VARCHAR);
                    attempt.setNull(4, Types.VARCHAR);
                } else {
                    attempt.setString(3, failure.getClass().getName());
                    attempt.setString(4, failure.getMessage());
                }
                attempt.setObject(5, lease.attemptId());
                attempt.executeUpdate();
            }
            if (lease.recurringOccurrenceId() != null) {
                short occurrenceStatus = switch (jobStatus) {
                    case WAITING -> 4;
                    case COMPLETED -> 5;
                    case CANCELLED -> 6;
                    case FAULTED -> 8;
                    default -> 3;
                };
                try (PreparedStatement occurrence = connection.prepareStatement("""
                        UPDATE myservicebus.recurring_job_occurrence
                        SET status = ?, failure_category = ? WHERE occurrence_id = ?
                        """)) {
                    occurrence.setShort(1, occurrenceStatus);
                    if (failure == null) {
                        occurrence.setNull(2, Types.VARCHAR);
                    } else {
                        occurrence.setString(2, failure.getClass().getName());
                    }
                    occurrence.setObject(3, lease.recurringOccurrenceId());
                    occurrence.executeUpdate();
                }
            }
            connection.commit();
        }
    }

    private void faultExhaustedLeases() throws SQLException {
        Instant now = clock.instant();
        try (Connection connection = dataSource.getConnection()) {
            connection.setAutoCommit(false);
            try (PreparedStatement attempts = connection.prepareStatement("""
                    UPDATE myservicebus.job_attempt attempt
                    SET status = 2, completed_at_utc = ?,
                        fault_type = 'MyServiceBus.JobLeaseExpired',
                        fault_message = 'The final worker lease expired before the attempt completed.'
                    FROM myservicebus.job job
                    WHERE attempt.job_id = job.job_id AND attempt.status = 0
                      AND job.service_name = ? AND job.status = 3
                      AND job.lease_expires_at_utc <= ? AND job.attempt_count > job.retry_limit
                    """)) {
                PostgreSqlJobProvider.setInstant(attempts, 1, now);
                attempts.setString(2, serviceName);
                PostgreSqlJobProvider.setInstant(attempts, 3, now);
                attempts.executeUpdate();
            }
            try (PreparedStatement occurrences = connection.prepareStatement("""
                    UPDATE myservicebus.recurring_job_occurrence occurrence
                    SET status = 8, failure_category = 'MyServiceBus.JobLeaseExpired'
                    FROM myservicebus.job job
                    WHERE job.recurring_occurrence_id = occurrence.occurrence_id
                      AND job.service_name = ? AND job.status = 3
                      AND job.lease_expires_at_utc <= ? AND job.attempt_count > job.retry_limit
                    """)) {
                occurrences.setString(1, serviceName);
                PostgreSqlJobProvider.setInstant(occurrences, 2, now);
                occurrences.executeUpdate();
            }
            try (PreparedStatement jobs = connection.prepareStatement("""
                    UPDATE myservicebus.job
                    SET status = 5, completed_at_utc = ?, updated_at_utc = ?,
                        lease_owner = NULL, lease_expires_at_utc = NULL,
                        failure_type = 'MyServiceBus.JobLeaseExpired',
                        failure_message = 'The final worker lease expired before the attempt completed.'
                    WHERE service_name = ? AND status = 3
                      AND lease_expires_at_utc <= ? AND attempt_count > retry_limit
                    """)) {
                PostgreSqlJobProvider.setInstant(jobs, 1, now);
                PostgreSqlJobProvider.setInstant(jobs, 2, now);
                jobs.setString(3, serviceName);
                PostgreSqlJobProvider.setInstant(jobs, 4, now);
                jobs.executeUpdate();
            }
            connection.commit();
        }
    }

    private static Throwable unwrap(Throwable failure) {
        Throwable current = failure;
        while ((current instanceof CompletionException || current instanceof ExecutionException)
                && current.getCause() != null) {
            current = current.getCause();
        }
        return current;
    }
}
