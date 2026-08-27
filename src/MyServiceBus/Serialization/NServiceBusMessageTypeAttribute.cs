namespace MyServiceBus.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class NServiceBusMessageTypeAttribute : Attribute
{
    public NServiceBusMessageTypeAttribute(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        TypeName = typeName;
    }

    public string TypeName { get; }
}
