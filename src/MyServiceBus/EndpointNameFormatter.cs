using System;
using System.Text.RegularExpressions;

namespace MyServiceBus;

public interface IEndpointNameFormatter
{
    string Format(Type messageType);
}

public class DefaultEndpointNameFormatter : IEndpointNameFormatter
{
    public static readonly DefaultEndpointNameFormatter Instance = new();

    public string Format(Type messageType)
    {
        return TrimConsumerSuffix(messageType.Name);
    }

    internal static string TrimConsumerSuffix(string name)
    {
        foreach (var suffix in new[] { "Consumer", "Saga", "Activity" })
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                return name[..^suffix.Length];
        }

        return name;
    }
}

public class KebabCaseEndpointNameFormatter : IEndpointNameFormatter
{
    public static readonly KebabCaseEndpointNameFormatter Instance = new();

    public string Format(Type messageType)
    {
        var name = DefaultEndpointNameFormatter.TrimConsumerSuffix(messageType.Name);
        return Regex.Replace(name, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
    }
}

public class SnakeCaseEndpointNameFormatter : IEndpointNameFormatter
{
    public static readonly SnakeCaseEndpointNameFormatter Instance = new();

    public string Format(Type messageType)
    {
        var name = DefaultEndpointNameFormatter.TrimConsumerSuffix(messageType.Name);
        return Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    }
}
