namespace MyServiceBus;

/// <summary>Defines the wire identity independently of the local type and broker entity name.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
public sealed class MessageUrnAttribute : Attribute
{
    public MessageUrnAttribute(string urn, bool useDefaultPrefix = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urn);
        Urn = useDefaultPrefix ? "urn:message:" + urn : urn;
        if (!Uri.TryCreate(Urn, UriKind.Absolute, out _))
            throw new ArgumentException("A message identity must be an absolute URN or URI.", nameof(urn));
    }
    public string Urn { get; }
}
