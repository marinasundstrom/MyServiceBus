using System.Text.RegularExpressions;

namespace MyServiceBus;

internal static partial class AmazonSqsEntityName
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidName();

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
        if (!ValidName().IsMatch(value))
            throw new ArgumentException("Amazon SQS/SNS entity names must contain 1-80 letters, digits, hyphens, or underscores.", nameof(value));
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

    public string FormatEntityName(Type messageType) =>
        AmazonSqsEntityName.Format(EntityNameFormatter.Format(messageType));
}
