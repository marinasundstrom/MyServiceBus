using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;
using MyServiceBus.Transports;
using Npgsql;
using NpgsqlTypes;

namespace MyServiceBus.Persistence.PostgreSql;

public sealed class PostgreSqlJobProcessor
{
    private sealed record Lease(
        Guid JobId,
        Guid AttemptId,
        int RetryAttempt,
        string JobTypeName,
        byte[] Body,
        string ContentType,
        int RetryLimit,
        long? RetryDelayMilliseconds,
        long TimeoutMilliseconds,
        DateTimeOffset StartedAtUtc);

    private sealed record StoredTransportMessage(
        IDictionary<string, object> Headers,
        bool IsDurable,
        byte[] Payload) : ITransportMessage;

    private static readonly MethodInfo ReadMessageMethod = typeof(PostgreSqlJobProcessor)
        .GetMethod(nameof(ReadMessage), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly NpgsqlDataSource dataSource;
    private readonly string serviceName;
    private readonly string workerId;
    private readonly IJobConsumerRegistry consumers;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IInboundMessageResolver inboundMessageResolver;
    private readonly PostgreSqlJobOptions options;
    private readonly TimeProvider timeProvider;

    public PostgreSqlJobProcessor(
        NpgsqlDataSource dataSource,
        string serviceName,
        IJobConsumerRegistry consumers,
        IServiceScopeFactory scopeFactory,
        IInboundMessageResolver inboundMessageResolver,
        PostgreSqlJobOptions options,
        TimeProvider? timeProvider = null)
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        this.serviceName = serviceName.Trim();
        this.consumers = consumers ?? throw new ArgumentNullException(nameof(consumers));
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.inboundMessageResolver = inboundMessageResolver
            ?? throw new ArgumentNullException(nameof(inboundMessageResolver));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    }

    public async Task<int> ProcessDueAsync(
        int? maximumCount = null,
        CancellationToken cancellationToken = default)
    {
        var count = maximumCount ?? options.BatchSize;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        await FaultExhaustedLeases(cancellationToken);
        var leases = new List<Lease>(count);
        for (var index = 0; index < count; index++)
        {
            var lease = await TryLease(cancellationToken);
            if (lease is null)
                break;
            leases.Add(lease);
        }

        await Task.WhenAll(leases.Select(lease => Execute(lease, cancellationToken)));
        return leases.Count;
    }

    private async Task<Lease?> TryLease(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var leaseExpiresAtUtc = now + options.LeaseDuration;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            WITH candidate AS (
                SELECT job_id
                FROM myservicebus.job
                WHERE service_name = @service_name
                  AND cancellation_requested_at_utc IS NULL
                  AND (
                      (status IN (1, 2) AND available_at_utc <= @now)
                      OR (status = 3 AND lease_expires_at_utc <= @now AND attempt_count <= retry_limit)
                  )
                ORDER BY available_at_utc, submitted_at_utc, job_id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE myservicebus.job job
            SET status = 3,
                started_at_utc = COALESCE(job.started_at_utc, @now),
                updated_at_utc = @now,
                lease_owner = @worker_id,
                lease_expires_at_utc = @lease_expires_at_utc,
                attempt_count = job.attempt_count + 1
            FROM candidate
            WHERE job.job_id = candidate.job_id
            RETURNING job.job_id, job.attempt_count - 1, job.job_type_name, job.body,
                job.content_type, job.retry_limit, job.retry_delay_milliseconds,
                job.timeout_milliseconds, @now;
            """, connection, transaction);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue("worker_id", NpgsqlDbType.Text, workerId);
        command.Parameters.AddWithValue("lease_expires_at_utc", NpgsqlDbType.TimestampTz, leaseExpiresAtUtc);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.CloseAsync();
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var jobId = reader.GetGuid(0);
        var retryAttempt = reader.GetInt32(1);
        var lease = new Lease(
            jobId,
            Guid.NewGuid(),
            retryAttempt,
            reader.GetString(2),
            reader.GetFieldValue<byte[]>(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetInt64(6),
            reader.GetInt64(7),
            reader.GetFieldValue<DateTimeOffset>(8));
        await reader.CloseAsync();

        await using (var staleAttempt = new NpgsqlCommand("""
            UPDATE myservicebus.job_attempt
            SET status = 2,
                completed_at_utc = @now,
                fault_type = 'MyServiceBus.JobLeaseExpired',
                fault_message = 'The worker lease expired before the attempt completed.'
            WHERE job_id = @job_id AND status = 0;
            """, connection, transaction))
        {
            staleAttempt.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
            staleAttempt.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, jobId);
            await staleAttempt.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var attempt = new NpgsqlCommand("""
            INSERT INTO myservicebus.job_attempt (
                attempt_id, job_id, retry_attempt, status, worker_id, started_at_utc)
            VALUES (@attempt_id, @job_id, @retry_attempt, 0, @worker_id, @now);
            """, connection, transaction))
        {
            attempt.Parameters.AddWithValue("attempt_id", NpgsqlDbType.Uuid, lease.AttemptId);
            attempt.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, lease.JobId);
            attempt.Parameters.AddWithValue("retry_attempt", NpgsqlDbType.Integer, lease.RetryAttempt);
            attempt.Parameters.AddWithValue("worker_id", NpgsqlDbType.Text, workerId);
            attempt.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
            await attempt.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return lease;
    }

    private async Task Execute(Lease lease, CancellationToken stoppingToken)
    {
        var descriptor = consumers.Get(lease.JobTypeName);
        await descriptor.Concurrency.WaitAsync(stoppingToken);
        try
        {
            var job = Deserialize(lease, descriptor.JobType);
            using var requestedCancellation = new CancellationTokenSource();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(lease.TimeoutMilliseconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken,
                requestedCancellation.Token,
                timeout.Token);
            using var scope = scopeFactory.CreateScope();
            var context = new JobExecutionContext(
                lease.JobId,
                lease.AttemptId,
                lease.RetryAttempt,
                job,
                linked.Token,
                lease.StartedAtUtc,
                progress => UpdateProgress(lease.JobId, progress, stoppingToken));
            var run = descriptor.Run(scope.ServiceProvider, context).WaitAsync(linked.Token);
            while (!run.IsCompleted)
            {
                var heartbeat = Task.Delay(options.HeartbeatInterval, stoppingToken);
                if (await Task.WhenAny(run, heartbeat) == run)
                    break;
                if (await RenewLease(lease.JobId, stoppingToken))
                    requestedCancellation.Cancel();
            }

            await run;
            var cancellationRequested = await IsCancellationRequested(lease.JobId, stoppingToken);
            await Finish(
                lease,
                cancellationRequested ? JobStatus.Cancelled : JobStatus.Completed,
                cancellationRequested ? JobAttemptStatus.Cancelled : JobAttemptStatus.Completed,
                null,
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Leave the durable lease for another process to recover.
        }
        catch (OperationCanceledException exception)
        {
            var cancellationRequested = await IsCancellationRequested(lease.JobId, CancellationToken.None);
            if (cancellationRequested)
            {
                await Finish(lease, JobStatus.Cancelled, JobAttemptStatus.Cancelled, null, CancellationToken.None);
            }
            else
            {
                var timeout = new TimeoutException(
                    $"Job '{lease.JobId}' exceeded its timeout of {TimeSpan.FromMilliseconds(lease.TimeoutMilliseconds)}.",
                    exception);
                await FailOrRetry(lease, timeout, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            await FailOrRetry(lease, exception, CancellationToken.None);
        }
        finally
        {
            descriptor.Concurrency.Release();
        }
    }

    private object Deserialize(Lease lease, Type jobType)
    {
        var transportMessage = new StoredTransportMessage(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [MassTransitHeaderConvention.Instance.ContentTypeHeader] = lease.ContentType
            },
            true,
            lease.Body);
        var inbound = inboundMessageResolver.Resolve(transportMessage);
        return ReadMessageMethod.MakeGenericMethod(jobType).Invoke(null, [inbound])
            ?? throw new InvalidOperationException($"The stored job '{lease.JobId}' could not be deserialized.");
    }

    private static object ReadMessage<TJob>(IInboundMessage inbound)
        where TJob : class =>
        inbound.TryGetMessage<TJob>(out var job) && job is not null
            ? job
            : throw new InvalidOperationException($"The stored payload does not contain {typeof(TJob)}.");

    private void UpdateProgress(Guid jobId, JobProgress progress, CancellationToken cancellationToken)
    {
        UpdateProgressAsync(jobId, progress, cancellationToken).GetAwaiter().GetResult();
    }

    private async Task UpdateProgressAsync(
        Guid jobId,
        JobProgress progress,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var command = dataSource.CreateCommand("""
            UPDATE myservicebus.job
            SET progress_value = @progress_value,
                progress_limit = @progress_limit,
                updated_at_utc = @now
            WHERE service_name = @service_name AND job_id = @job_id
              AND status = 3 AND lease_owner = @worker_id;
            """);
        command.Parameters.AddWithValue("progress_value", NpgsqlDbType.Bigint, progress.Value);
        command.Parameters.AddWithValue(
            "progress_limit",
            NpgsqlDbType.Bigint,
            progress.Limit is null ? DBNull.Value : progress.Limit.Value);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, jobId);
        command.Parameters.AddWithValue("worker_id", NpgsqlDbType.Text, workerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<bool> RenewLease(Guid jobId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var command = dataSource.CreateCommand("""
            UPDATE myservicebus.job
            SET lease_expires_at_utc = @lease_expires_at_utc,
                updated_at_utc = @now
            WHERE service_name = @service_name AND job_id = @job_id
              AND status = 3 AND lease_owner = @worker_id
            RETURNING cancellation_requested_at_utc IS NOT NULL;
            """);
        command.Parameters.AddWithValue("lease_expires_at_utc", NpgsqlDbType.TimestampTz, now + options.LeaseDuration);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, jobId);
        command.Parameters.AddWithValue("worker_id", NpgsqlDbType.Text, workerId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private async Task<bool> IsCancellationRequested(Guid jobId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT cancellation_requested_at_utc IS NOT NULL
            FROM myservicebus.job
            WHERE service_name = @service_name AND job_id = @job_id;
            """);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, jobId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private Task FailOrRetry(Lease lease, Exception exception, CancellationToken cancellationToken)
    {
        var retry = lease.RetryAttempt < lease.RetryLimit;
        return Finish(
            lease,
            retry ? JobStatus.Waiting : JobStatus.Faulted,
            JobAttemptStatus.Faulted,
            exception,
            cancellationToken,
            retry ? lease.RetryDelayMilliseconds : null);
    }

    private async Task Finish(
        Lease lease,
        JobStatus jobStatus,
        JobAttemptStatus attemptStatus,
        Exception? exception,
        CancellationToken cancellationToken,
        long? retryDelayMilliseconds = null)
    {
        var now = timeProvider.GetUtcNow();
        var availableAtUtc = retryDelayMilliseconds is null
            ? now
            : now + TimeSpan.FromMilliseconds(retryDelayMilliseconds.Value);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var job = new NpgsqlCommand("""
            UPDATE myservicebus.job
            SET status = @status,
                completed_at_utc = CASE WHEN @status IN (4, 5, 6) THEN @now ELSE NULL END,
                available_at_utc = @available_at_utc,
                updated_at_utc = @now,
                lease_owner = NULL,
                lease_expires_at_utc = NULL,
                failure_type = @failure_type,
                failure_message = @failure_message
            WHERE service_name = @service_name AND job_id = @job_id
              AND status = 3 AND lease_owner = @worker_id;
            """, connection, transaction))
        {
            job.Parameters.AddWithValue("status", NpgsqlDbType.Smallint, (short)jobStatus);
            job.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
            job.Parameters.AddWithValue("available_at_utc", NpgsqlDbType.TimestampTz, availableAtUtc);
            job.Parameters.AddWithValue("failure_type", NpgsqlDbType.Text, exception?.GetType().FullName ?? (object)DBNull.Value);
            job.Parameters.AddWithValue("failure_message", NpgsqlDbType.Text, exception?.Message ?? (object)DBNull.Value);
            job.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
            job.Parameters.AddWithValue("job_id", NpgsqlDbType.Uuid, lease.JobId);
            job.Parameters.AddWithValue("worker_id", NpgsqlDbType.Text, workerId);
            if (await job.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }
        }

        await using (var attempt = new NpgsqlCommand("""
            UPDATE myservicebus.job_attempt
            SET status = @status,
                completed_at_utc = @now,
                fault_type = @fault_type,
                fault_message = @fault_message
            WHERE attempt_id = @attempt_id AND status = 0;
            """, connection, transaction))
        {
            attempt.Parameters.AddWithValue("status", NpgsqlDbType.Smallint, (short)attemptStatus);
            attempt.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
            attempt.Parameters.AddWithValue("fault_type", NpgsqlDbType.Text, exception?.GetType().FullName ?? (object)DBNull.Value);
            attempt.Parameters.AddWithValue("fault_message", NpgsqlDbType.Text, exception?.Message ?? (object)DBNull.Value);
            attempt.Parameters.AddWithValue("attempt_id", NpgsqlDbType.Uuid, lease.AttemptId);
            await attempt.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task FaultExhaustedLeases(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var attempts = new NpgsqlCommand("""
            UPDATE myservicebus.job_attempt attempt
            SET status = 2,
                completed_at_utc = @now,
                fault_type = 'MyServiceBus.JobLeaseExpired',
                fault_message = 'The final worker lease expired before the attempt completed.'
            FROM myservicebus.job job
            WHERE attempt.job_id = job.job_id AND attempt.status = 0
              AND job.service_name = @service_name AND job.status = 3
              AND job.lease_expires_at_utc <= @now AND job.attempt_count > job.retry_limit;
            """, connection, transaction))
        {
            attempts.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
            attempts.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
            await attempts.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var jobs = new NpgsqlCommand("""
            UPDATE myservicebus.job
            SET status = 5,
                completed_at_utc = @now,
                updated_at_utc = @now,
                lease_owner = NULL,
                lease_expires_at_utc = NULL,
                failure_type = 'MyServiceBus.JobLeaseExpired',
                failure_message = 'The final worker lease expired before the attempt completed.'
            WHERE service_name = @service_name AND status = 3
              AND lease_expires_at_utc <= @now AND attempt_count > retry_limit;
            """, connection, transaction))
        {
            jobs.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
            jobs.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
            await jobs.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
