using System.Globalization;
using MyServiceBus.Serialization;
using Npgsql;
using NpgsqlTypes;

namespace MyServiceBus.Persistence.PostgreSql;

internal sealed class PostgreSqlJobProvider : IJobProvider
{
    private readonly NpgsqlDataSource dataSource;
    private readonly string serviceName;
    private readonly IJobConsumerRegistry consumers;
    private readonly IMessageSerializer serializer;
    private readonly TimeProvider timeProvider;

    public PostgreSqlJobProvider(
        NpgsqlDataSource dataSource,
        string serviceName,
        IJobConsumerRegistry consumers,
        IMessageSerializer serializer,
        TimeProvider? timeProvider = null)
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        this.serviceName = serviceName.Trim();
        this.consumers = consumers ?? throw new ArgumentNullException(nameof(consumers));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string ProviderName => "MyServiceBus.Durable";

    public SchedulingDurability Durability => SchedulingDurability.Durable;

    public SchedulingPlacement Placement => SchedulingPlacement.Embedded;

    string IJobSource.Provider => ProviderName;

    public bool Authoritative => true;

    public Task<JobSubmissionReceipt> Submit<TJob>(
        TJob job,
        JobSubmissionOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : class =>
        Store(job, options, null, cancellationToken);

    public Task<JobSubmissionReceipt> Schedule<TJob>(
        DateTimeOffset startAtUtc,
        TJob job,
        JobSubmissionOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : class =>
        Store(job, options, startAtUtc.ToUniversalTime(), cancellationToken);

    public async Task<JobControlResult> Cancel(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var status = await ReadStatus(connection, transaction, jobId, cancellationToken);
        if (status is null)
            return new JobControlResult(JobControlOutcome.NotFound);
        if (status is JobStatus.Completed or JobStatus.Faulted or JobStatus.Cancelled)
            return new JobControlResult(JobControlOutcome.Unchanged, status);

        var now = timeProvider.GetUtcNow();
        var nextStatus = status is JobStatus.Submitted or JobStatus.Scheduled or JobStatus.Waiting
            ? JobStatus.Cancelled
            : status.Value;
        await using var command = new NpgsqlCommand("""
            UPDATE myservicebus.job
            SET cancellation_requested_at_utc = @now,
                status = @status,
                completed_at_utc = CASE WHEN @status = 6 THEN @now ELSE completed_at_utc END,
                updated_at_utc = @now
            WHERE service_name = @service_name AND job_id = @job_id;
            """, connection, transaction);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue("status", NpgsqlDbType.Smallint, (short)nextStatus);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, jobId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new JobControlResult(JobControlOutcome.Applied, nextStatus);
    }

    public async Task<JobControlResult> Retry(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        await using var command = dataSource.CreateCommand("""
            UPDATE myservicebus.job
            SET status = 2,
                available_at_utc = @now,
                completed_at_utc = NULL,
                cancellation_requested_at_utc = NULL,
                lease_owner = NULL,
                lease_expires_at_utc = NULL,
                failure_type = NULL,
                failure_message = NULL,
                updated_at_utc = @now
            WHERE service_name = @service_name AND job_id = @job_id AND status IN (5, 6)
            RETURNING status;
            """);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, jobId);
        var updated = await command.ExecuteScalarAsync(cancellationToken);
        if (updated is not null)
            return new JobControlResult(JobControlOutcome.Applied, JobStatus.Waiting);

        var current = await ReadStatus(jobId, cancellationToken);
        return current is null
            ? new JobControlResult(JobControlOutcome.NotFound)
            : new JobControlResult(JobControlOutcome.InvalidState, current);
    }

    public async Task<IReadOnlyList<JobState>> GetSnapshotAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        await using var command = dataSource.CreateCommand("""
            SELECT job_id, job_type_name, status, submitted_at_utc, scheduled_for_utc,
                started_at_utc, completed_at_utc, progress_value, progress_limit,
                recurring_occurrence_id, updated_at_utc
            FROM myservicebus.job
            WHERE service_name = @service_name
            ORDER BY updated_at_utc DESC, job_id
            LIMIT @maximum_count;
            """);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("maximum_count", NpgsqlDbType.Integer, maximumCount);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var jobs = new List<JobState>();
        while (await reader.ReadAsync(cancellationToken))
        {
            JobProgress? progress = reader.IsDBNull(7)
                ? null
                : new JobProgress(reader.GetInt64(7), reader.IsDBNull(8) ? null : reader.GetInt64(8));
            jobs.Add(new JobState(
                reader.GetGuid(0),
                reader.GetString(1),
                (JobStatus)reader.GetInt16(2),
                ProviderName,
                Durability,
                Placement,
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
                progress,
                reader.IsDBNull(9) ? null : reader.GetGuid(9),
                reader.GetFieldValue<DateTimeOffset>(10)));
        }
        return jobs;
    }

    public async Task<IReadOnlyList<JobAttemptState>> GetAttemptsAsync(
        Guid jobId,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        await using var command = dataSource.CreateCommand("""
            SELECT attempt_id, job_id, retry_attempt, status, started_at_utc,
                completed_at_utc, fault_type, fault_message
            FROM myservicebus.job_attempt
            WHERE job_id = @job_id
              AND EXISTS (
                  SELECT 1 FROM myservicebus.job
                  WHERE job.job_id = job_attempt.job_id AND service_name = @service_name)
            ORDER BY retry_attempt DESC
            LIMIT @maximum_count;
            """);
        command.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, jobId);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("maximum_count", NpgsqlDbType.Integer, maximumCount);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var attempts = new List<JobAttemptState>();
        while (await reader.ReadAsync(cancellationToken))
        {
            attempts.Add(new JobAttemptState(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt32(2),
                (JobAttemptStatus)reader.GetInt16(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }
        attempts.Reverse();
        return attempts;
    }

    private async Task<JobSubmissionReceipt> Store<TJob>(
        TJob job,
        JobSubmissionOptions? options,
        DateTimeOffset? scheduledForUtc,
        CancellationToken cancellationToken)
        where TJob : class
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();
        var descriptor = consumers.Get(typeof(TJob));
        var now = timeProvider.GetUtcNow();
        if (scheduledForUtc is { } scheduled && scheduled < now)
            scheduledForUtc = now;
        var jobId = options?.JobId ?? Guid.NewGuid();
        var status = scheduledForUtc is null ? JobStatus.Waiting : JobStatus.Scheduled;
        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(TJob)), serializer, cancellationToken)
        {
            MessageId = jobId.ToString(),
            DestinationAddress = new Uri($"loopback://localhost/jobs/{Uri.EscapeDataString(descriptor.JobTypeName)}")
        };
        var body = context.GetMessageBody(job).GetBytes();
        var messageTypes = MessageTypeCache.GetMessageTypes(typeof(TJob)).Select(MessageUrn.For).ToArray();

        await using var command = dataSource.CreateCommand("""
            INSERT INTO myservicebus.job (
                job_id, service_name, job_type_name, message_types, body, content_type, headers,
                status, submitted_at_utc, scheduled_for_utc, available_at_utc, updated_at_utc,
                retry_limit, retry_delay_milliseconds, timeout_milliseconds, concurrent_job_limit)
            VALUES (
                @job_id, @service_name, @job_type_name, @message_types, @body, @content_type, '{}'::jsonb,
                @status, @now, @scheduled_for_utc, @available_at_utc, @now,
                @retry_limit, @retry_delay_milliseconds, @timeout_milliseconds, @concurrent_job_limit);
            """);
        command.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, jobId);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("job_type_name", NpgsqlDbType.Text, descriptor.JobTypeName);
        command.Parameters.AddWithValue("message_types", NpgsqlDbType.Array | NpgsqlDbType.Text, messageTypes);
        command.Parameters.AddWithValue("body", NpgsqlDbType.Bytea, body);
        command.Parameters.AddWithValue("content_type", NpgsqlDbType.Text, serializer.ContentType);
        command.Parameters.AddWithValue("status", NpgsqlDbType.Smallint, (short)status);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue(
            "scheduled_for_utc",
            NpgsqlDbType.TimestampTz,
            scheduledForUtc is null ? DBNull.Value : scheduledForUtc.Value);
        command.Parameters.AddWithValue("available_at_utc", NpgsqlDbType.TimestampTz, scheduledForUtc ?? now);
        command.Parameters.AddWithValue("retry_limit", NpgsqlDbType.Integer, descriptor.Options.RetryCount);
        command.Parameters.AddWithValue(
            "retry_delay_milliseconds",
            NpgsqlDbType.Bigint,
            descriptor.Options.RetryDelay is null
                ? DBNull.Value
                : checked((long)descriptor.Options.RetryDelay.Value.TotalMilliseconds));
        command.Parameters.AddWithValue(
            "timeout_milliseconds",
            NpgsqlDbType.Bigint,
            checked((long)descriptor.Options.JobTimeout.TotalMilliseconds));
        command.Parameters.AddWithValue(
            "concurrent_job_limit",
            NpgsqlDbType.Integer,
            descriptor.Options.ConcurrentJobLimit);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException($"Job '{jobId}' already exists.", exception);
        }
        return new JobSubmissionReceipt(jobId, status, now, scheduledForUtc);
    }

    private async Task<JobStatus?> ReadStatus(Guid jobId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT status FROM myservicebus.job
            WHERE service_name = @service_name AND job_id = @job_id;
            """);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, jobId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : (JobStatus)Convert.ToInt16(value, CultureInfo.InvariantCulture);
    }

    private async Task<JobStatus?> ReadStatus(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT status FROM myservicebus.job
            WHERE service_name = @service_name AND job_id = @job_id
            FOR UPDATE;
            """, connection, transaction);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, jobId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : (JobStatus)Convert.ToInt16(value, CultureInfo.InvariantCulture);
    }
}
