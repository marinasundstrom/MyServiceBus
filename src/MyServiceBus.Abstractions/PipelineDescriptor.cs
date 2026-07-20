using System.Collections.ObjectModel;

namespace MyServiceBus;

public enum FilterLifetime
{
    Instance,
    Pipe,
    Scoped
}

public sealed record FilterDescriptor
{
    public FilterDescriptor(
        int order,
        string kind,
        string? implementation,
        FilterLifetime lifetime,
        IReadOnlyDictionary<string, string>? configuration = null)
    {
        Order = order;
        Kind = kind;
        Implementation = implementation;
        Lifetime = lifetime;
        Configuration = new ReadOnlyDictionary<string, string>(
            configuration is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(configuration, StringComparer.Ordinal));
    }

    public int Order { get; }
    public string Kind { get; }
    public string? Implementation { get; }
    public FilterLifetime Lifetime { get; }
    public IReadOnlyDictionary<string, string> Configuration { get; }
}

public sealed record PipelineDescriptor
{
    public const int CurrentVersion = 1;

    public PipelineDescriptor(IReadOnlyList<FilterDescriptor> filters)
    {
        Version = CurrentVersion;
        Filters = Array.AsReadOnly(filters.ToArray());
    }

    public int Version { get; }
    public IReadOnlyList<FilterDescriptor> Filters { get; }
}
