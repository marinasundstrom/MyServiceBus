namespace MyServiceBus;

internal enum AmazonSqsEntityKind
{
    Queue,
    Topic
}

internal readonly record struct AmazonSqsEndpointAddress(string EntityName, AmazonSqsEntityKind Kind)
{
    public static AmazonSqsEndpointAddress Parse(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.Scheme.Equals("amazonsqs", StringComparison.OrdinalIgnoreCase) &&
            !address.Scheme.Equals("queue", StringComparison.OrdinalIgnoreCase) &&
            !address.Scheme.Equals("topic", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported Amazon SQS address scheme '{address.Scheme}'.", nameof(address));

        var logicalScheme = address.Scheme.Equals("queue", StringComparison.OrdinalIgnoreCase) ||
                            address.Scheme.Equals("topic", StringComparison.OrdinalIgnoreCase);
        var name = logicalScheme
            ? address.OriginalString[(address.Scheme.Length + 1)..].Split('?', 2)[0].TrimStart('/')
            : address.Segments.LastOrDefault()?.Trim('/');
        AmazonSqsEntityName.Validate(name ?? string.Empty);

        var query = System.Web.HttpUtility.ParseQueryString(address.Query);
        var kind = address.Scheme.Equals("topic", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(query["type"], "topic", StringComparison.OrdinalIgnoreCase)
            ? AmazonSqsEntityKind.Topic
            : AmazonSqsEntityKind.Queue;
        return new AmazonSqsEndpointAddress(name!, kind);
    }
}
