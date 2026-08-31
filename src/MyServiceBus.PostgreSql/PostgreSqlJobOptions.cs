namespace MyServiceBus.Persistence.PostgreSql;

public sealed class PostgreSqlJobOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 16;

    internal void Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("The PostgreSQL job poll interval must be greater than zero.");
        if (LeaseDuration <= TimeSpan.Zero)
            throw new InvalidOperationException("The PostgreSQL job lease duration must be greater than zero.");
        if (HeartbeatInterval <= TimeSpan.Zero || HeartbeatInterval >= LeaseDuration)
            throw new InvalidOperationException("The PostgreSQL job heartbeat interval must be greater than zero and shorter than the lease duration.");
        if (BatchSize <= 0)
            throw new InvalidOperationException("The PostgreSQL job batch size must be greater than zero.");
    }
}
