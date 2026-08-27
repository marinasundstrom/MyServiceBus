namespace MyServiceBus.Serialization;

public static class NServiceBusHeaders
{
    public const string ContentType = "NServiceBus.ContentType";
    public const string EnclosedMessageTypes = "NServiceBus.EnclosedMessageTypes";
    public const string MessageId = "NServiceBus.MessageId";
    public const string MessageIntent = "NServiceBus.MessageIntent";
    public const string CorrelationId = "NServiceBus.CorrelationId";
    public const string ConversationId = "NServiceBus.ConversationId";
    public const string ReplyToAddress = "NServiceBus.ReplyToAddress";
    public const string RelatedTo = "NServiceBus.RelatedTo";
    public const string TimeSent = "NServiceBus.TimeSent";
}
