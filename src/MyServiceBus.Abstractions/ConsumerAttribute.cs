namespace MyServiceBus;

/// <summary>
/// Declares a method as a message consumer, or declares the eligible methods on a containing class.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ConsumerAttribute : Attribute
{
    /// <summary>
    /// Creates a consumer declaration using its default endpoint name.
    /// </summary>
    public ConsumerAttribute()
    {
    }

    /// <summary>
    /// Creates a consumer declaration for the specified endpoint.
    /// </summary>
    /// <param name="endpointName">The receive endpoint name.</param>
    public ConsumerAttribute(string endpointName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        EndpointName = endpointName;
    }

    /// <summary>
    /// Gets the explicitly configured endpoint name, if any.
    /// </summary>
    public string? EndpointName { get; }
}
