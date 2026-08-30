using System.Text.RegularExpressions;

namespace MyServiceBus;

internal static partial class AmazonSqsEntityName
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidQueueName();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,256}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidTopicName();

    public static string Format(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = InvalidCharacters().Replace(value, "-").Trim('-');
        if (normalized.Length > 80)
            normalized = normalized[..80];
        Validate(normalized);
        return normalized;
    }

    public static void Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!ValidQueueName().IsMatch(value))
            throw new ArgumentException("Amazon SQS queue names must contain 1-80 letters, digits, hyphens, or underscores.", nameof(value));
    }

    public static string FormatTopic(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = InvalidCharacters().Replace(value, "-");
        ValidateTopic(normalized);
        return normalized;
    }

    public static void ValidateTopic(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!ValidTopicName().IsMatch(value))
            throw new ArgumentException("Amazon SNS topic names must contain 1-256 letters, digits, hyphens, or underscores.", nameof(value));
    }

    public static string Companion(string value, string suffix)
    {
        Validate(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        var length = 80 - suffix.Length;
        if (length < 1)
            throw new ArgumentException("Amazon SQS companion suffix is too long.", nameof(suffix));
        return (value.Length > length ? value[..length] : value) + suffix;
    }

    [GeneratedRegex("[^A-Za-z0-9_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidCharacters();
}

internal sealed class AmazonSqsMessageEntityNameFormatter : IMessageEntityNameFormatter
{
    public static AmazonSqsMessageEntityNameFormatter Instance { get; } = new();

    public string FormatEntityName<T>() => FormatEntityName(typeof(T));

    public string FormatEntityName(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        var configuredName = messageType.GetCustomAttributes(typeof(EntityNameAttribute), false)
            .Cast<EntityNameAttribute>()
            .FirstOrDefault()?.EntityName;
        return configuredName is not null
            ? AmazonSqsEntityName.FormatTopic(configuredName)
            : FormatMessageName(messageType, null);
    }

    private static string FormatMessageName(Type messageType, string? scope)
    {
        if (messageType.IsGenericParameter)
            return string.Empty;
        if (messageType.IsGenericTypeDefinition)
            throw new ArgumentException("An open generic type cannot be used as an Amazon SQS message name.", nameof(messageType));

        var result = new System.Text.StringBuilder();
        var messageNamespace = messageType.Namespace?.Replace('.', '_');
        if (messageNamespace is not null && !messageNamespace.Equals(scope, StringComparison.Ordinal))
            result.Append(messageNamespace).Append('-');

        if (messageType is { IsNested: true, DeclaringType: not null })
            result.Append(FormatMessageName(messageType.DeclaringType, messageNamespace)).Append('_');

        if (messageType.IsGenericType)
        {
            var name = messageType.GetGenericTypeDefinition().Name;
            var arity = name.IndexOf('`');
            result.Append(arity > 0 ? name[..arity] : name).Append("--");
            var arguments = messageType.GetGenericArguments();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (index > 0)
                    result.Append("__");
                result.Append(FormatMessageName(arguments[index], messageNamespace));
            }
            result.Append("--");
        }
        else
            result.Append(messageType.Name);

        return AmazonSqsEntityName.FormatTopic(result.ToString());
    }
}
