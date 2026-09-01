namespace MyServiceBus.Orchestration;

/// <summary>
/// Provides atomic, correlation-scoped access to saga instances.
/// </summary>
/// <typeparam name="TSaga">The application-owned saga data type.</typeparam>
public interface ISagaRepository<TSaga>
    where TSaga : class
{
    SagaRepositoryCapabilities Capabilities { get; }

    ValueTask<TResult> Execute<TResult>(
        Guid correlationId,
        Func<TSaga?, CancellationToken, ValueTask<SagaRepositoryTransaction<TSaga, TResult>>> execute,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes the behavior a saga repository provider can guarantee.
/// </summary>
public sealed record SagaRepositoryCapabilities
{
    public SagaRepositoryCapabilities(
        string provider,
        SagaCorrelationKind correlation,
        SagaConcurrencyKind concurrency,
        SagaDurabilityKind durability,
        SagaOutboxKind outbox,
        bool finalInstanceDeletion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        if (!Enum.IsDefined(correlation) || !Enum.IsDefined(concurrency) ||
            !Enum.IsDefined(durability) || !Enum.IsDefined(outbox))
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlation),
                "Saga repository capabilities cannot contain an unknown value.");
        }

        Provider = provider;
        Correlation = correlation;
        Concurrency = concurrency;
        Durability = durability;
        Outbox = outbox;
        FinalInstanceDeletion = finalInstanceDeletion;
    }

    public string Provider { get; }
    public SagaCorrelationKind Correlation { get; }
    public SagaConcurrencyKind Concurrency { get; }
    public SagaDurabilityKind Durability { get; }
    public SagaOutboxKind Outbox { get; }
    public bool FinalInstanceDeletion { get; }

    public void EnsureSupports(
        SagaRepositoryRequirements requirements,
        SagaCompletionPolicy completionPolicy)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        var unsupported = new List<string>();
        if (Correlation != requirements.Correlation)
            unsupported.Add($"correlation '{requirements.Correlation}'");
        if (requirements.Concurrency != SagaConcurrencyKind.SingleProcess && Concurrency != requirements.Concurrency)
            unsupported.Add($"concurrency '{requirements.Concurrency}'");
        if (requirements.Durability == SagaDurabilityKind.Durable && Durability != SagaDurabilityKind.Durable)
            unsupported.Add("durable storage");
        if (requirements.Outbox == SagaOutboxKind.Transactional && Outbox != SagaOutboxKind.Transactional)
            unsupported.Add("transactional outbox");
        if (completionPolicy == SagaCompletionPolicy.DeleteWhenFinalized && !FinalInstanceDeletion)
            unsupported.Add("final-instance deletion");

        if (unsupported.Count > 0)
            throw new SagaRepositoryCapabilityException(Provider, unsupported);
    }
}

/// <summary>
/// Describes the repository mutation to commit after a saga behavior completes.
/// </summary>
public sealed record SagaRepositoryTransaction<TSaga, TResult>
    where TSaga : class
{
    public SagaRepositoryTransaction(
        SagaRepositoryMutation mutation,
        TSaga? instance,
        TResult result)
    {
        if (!Enum.IsDefined(mutation))
            throw new ArgumentOutOfRangeException(nameof(mutation));
        if (mutation == SagaRepositoryMutation.Upsert && instance is null)
            throw new ArgumentNullException(nameof(instance), "An upsert mutation requires a saga instance.");

        Mutation = mutation;
        Instance = instance;
        Result = result;
    }

    public SagaRepositoryMutation Mutation { get; }
    public TSaga? Instance { get; }
    public TResult Result { get; }

    public static SagaRepositoryTransaction<TSaga, TResult> NoChange(TResult result)
        => new(SagaRepositoryMutation.None, null, result);

    public static SagaRepositoryTransaction<TSaga, TResult> Upsert(TSaga instance, TResult result)
        => new(SagaRepositoryMutation.Upsert, instance ?? throw new ArgumentNullException(nameof(instance)), result);

    public static SagaRepositoryTransaction<TSaga, TResult> Delete(TResult result)
        => new(SagaRepositoryMutation.Delete, null, result);
}

public enum SagaRepositoryMutation
{
    None,
    Upsert,
    Delete
}

/// <summary>
/// Raised when a state-machine definition requires behavior its configured repository cannot provide.
/// </summary>
public sealed class SagaRepositoryCapabilityException : InvalidOperationException
{
    public SagaRepositoryCapabilityException(string provider, IReadOnlyCollection<string> unsupportedCapabilities)
        : base($"Saga repository provider '{provider}' does not support: {string.Join(", ", unsupportedCapabilities)}.")
    {
        Provider = provider;
        UnsupportedCapabilities = unsupportedCapabilities.ToArray();
    }

    public string Provider { get; }
    public IReadOnlyList<string> UnsupportedCapabilities { get; }
}
