namespace MyServiceBus.Persistence;

using System.Collections.ObjectModel;

public enum OutboxDeliveryIntent
{
    Send,
    Publish,
    Reply,
    Fault
}

public enum OutboxMessageState
{
    Pending,
    Leased,
    Dispatched,
    Dead,
    Cancelled
}

public sealed class OutboxMessage
{
    private readonly byte[] body;

    public OutboxMessage(
        Guid recordId,
        Guid messageId,
        OutboxDeliveryIntent intent,
        Uri destinationAddress,
        IReadOnlyList<string> messageTypes,
        ReadOnlySpan<byte> body,
        string contentType,
        IReadOnlyDictionary<string, string> headers,
        DateTimeOffset createdAtUtc,
        Guid? requestId = null,
        Guid? correlationId = null,
        Guid? conversationId = null,
        Guid? initiatorId = null,
        Uri? responseAddress = null,
        Uri? faultAddress = null,
        DateTimeOffset? availableAtUtc = null,
        DateTimeOffset? scheduledAtUtc = null,
        Guid? causationMessageId = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(recordId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(messageId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(destinationAddress);
        ArgumentNullException.ThrowIfNull(messageTypes);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(headers);

        if (messageTypes.Count == 0 || messageTypes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty message type is required.", nameof(messageTypes));

        RecordId = recordId;
        MessageId = messageId;
        Intent = intent;
        DestinationAddress = destinationAddress;
        MessageTypes = Array.AsReadOnly(messageTypes.ToArray());
        this.body = body.ToArray();
        ContentType = contentType;
        Headers = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(headers));
        CreatedAtUtc = createdAtUtc;
        RequestId = requestId;
        CorrelationId = correlationId;
        ConversationId = conversationId;
        InitiatorId = initiatorId;
        ResponseAddress = responseAddress;
        FaultAddress = faultAddress;
        AvailableAtUtc = availableAtUtc ?? createdAtUtc;
        ScheduledAtUtc = scheduledAtUtc;
        CausationMessageId = causationMessageId;
    }

    public Guid RecordId { get; }
    public Guid MessageId { get; }
    public OutboxDeliveryIntent Intent { get; }
    public Uri DestinationAddress { get; }
    public IReadOnlyList<string> MessageTypes { get; }
    public ReadOnlyMemory<byte> Body => body.ToArray();
    public string ContentType { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public Guid? RequestId { get; }
    public Guid? CorrelationId { get; }
    public Guid? ConversationId { get; }
    public Guid? InitiatorId { get; }
    public Uri? ResponseAddress { get; }
    public Uri? FaultAddress { get; }
    public DateTimeOffset AvailableAtUtc { get; }
    public DateTimeOffset? ScheduledAtUtc { get; }
    public Guid? CausationMessageId { get; }
}
