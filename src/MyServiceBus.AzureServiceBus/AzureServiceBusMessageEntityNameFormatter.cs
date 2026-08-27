using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace MyServiceBus;

/// <summary>
/// Formats message entity names using the MassTransit Azure Service Bus convention.
/// </summary>
public sealed class AzureServiceBusMessageEntityNameFormatter : IMessageEntityNameFormatter
{
    private readonly ConcurrentDictionary<Type, string> _cache = new();

    public static AzureServiceBusMessageEntityNameFormatter Instance { get; } = new();

    private AzureServiceBusMessageEntityNameFormatter()
    {
    }

    public string FormatEntityName(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        var attribute = messageType.GetCustomAttribute<EntityNameAttribute>();
        return attribute?.EntityName ?? _cache.GetOrAdd(messageType, Format);
    }

    private static string Format(Type messageType)
    {
        if (messageType.IsGenericTypeDefinition)
            throw new ArgumentException("An open generic type cannot be used as a message name.", nameof(messageType));

        return Append(new StringBuilder(), messageType, null).ToString().Replace("[]", "__", StringComparison.Ordinal);
    }

    private static StringBuilder Append(StringBuilder builder, Type messageType, string? scope)
    {
        if (messageType.IsGenericParameter)
            return builder;

        var messageNamespace = messageType.Namespace;
        if (messageNamespace is not null && !string.Equals(messageNamespace, scope, StringComparison.Ordinal))
            builder.Append(messageNamespace).Append('/');

        if (messageType.IsNested)
            Append(builder, messageType.DeclaringType!, messageNamespace).Append('-');

        if (!messageType.IsGenericType)
            return builder.Append(messageType.Name);

        var name = messageType.GetGenericTypeDefinition().Name;
        var arityIndex = name.IndexOf('`', StringComparison.Ordinal);
        builder.Append(arityIndex > 0 ? name[..arityIndex] : name).Append("--");

        var arguments = messageType.GetGenericArguments();
        for (var index = 0; index < arguments.Length; index++)
        {
            if (index > 0)
                builder.Append("---");
            Append(builder, arguments[index], messageNamespace);
        }

        return builder.Append("--");
    }
}
