namespace MyServiceBus;

/// <summary>Bus-local wire contract identities, frozen when configuration completes.</summary>
public sealed class MessageContractRegistry
{
    private readonly Dictionary<Type, string> overrides = new();
    private readonly Dictionary<string, Type> owners = new(StringComparer.Ordinal);
    private readonly object sync = new();
    private bool frozen;

    /// <summary>Overrides a closed message contract before configuration is frozen.</summary>
    /// <exception cref="ArgumentException">The type or identity is invalid.</exception>
    /// <exception cref="InvalidOperationException">Configuration has been frozen.</exception>
    public void SetMessageUrn(Type messageType, string urn)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(urn);
        if (messageType.IsValueType || messageType.ContainsGenericParameters)
            throw new ArgumentException("A closed reference contract is required.", nameof(messageType));
        if (!Uri.TryCreate(urn, UriKind.Absolute, out _))
            throw new ArgumentException("A message identity must be an absolute URN or URI.", nameof(urn));
        lock (sync)
        {
            if (frozen) throw new InvalidOperationException("Message contracts are frozen.");
            overrides[messageType] = urn;
        }
    }

    /// <summary>Resolves and validates the bus-local identity.</summary>
    /// <exception cref="InvalidOperationException">Another local contract owns this identity.</exception>
    /// <exception cref="ArgumentException">The contract identity is invalid.</exception>
    public string GetMessageUrn(Type messageType)
    {
        lock (sync)
        {
            var urn = overrides.TryGetValue(messageType, out var configured) ? configured : DefaultUrn(messageType);
            if (!Uri.TryCreate(urn, UriKind.Absolute, out _))
                throw new ArgumentException($"Message identity for {messageType} must be an absolute URN or URI.");
            if (owners.TryGetValue(urn, out var owner) && owner != messageType)
                throw new InvalidOperationException($"Message identity '{urn}' is shared by {owner} and {messageType}.");
            owners[urn] = messageType;
            return urn;
        }
    }

    /// <summary>Validates registered contracts and prevents subsequent overrides.</summary>
    /// <exception cref="InvalidOperationException">Registered contracts have duplicate identities.</exception>
    public void Freeze(IEnumerable<Type> messageTypes)
    {
        lock (sync)
        {
            if (frozen) return;
            owners.Clear();
            foreach (var type in messageTypes.Concat(overrides.Keys).SelectMany(MessageTypeCache.GetMessageTypes).Distinct())
                GetMessageUrn(type);
            frozen = true;
        }
    }

    private string DefaultUrn(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Fault<>))
        {
            var inner = GetMessageUrn(type.GetGenericArguments()[0]);
            return $"urn:message:MassTransit:Fault[[{(inner.StartsWith("urn:message:", StringComparison.Ordinal) ? inner[12..] : inner)}]]";
        }
        return MessageUrn.For(type);
    }
}

internal sealed class MessageContractSendFilter(MessageContractRegistry contracts) : IFilter<SendContext>
{
    public Task Send(SendContext context, IPipe<SendContext> next)
    {
        context.MessageUrnResolver = contracts.GetMessageUrn;
        return next.Send(context);
    }
}
