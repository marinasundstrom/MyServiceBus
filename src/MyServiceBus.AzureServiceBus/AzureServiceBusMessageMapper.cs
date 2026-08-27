using Azure.Messaging.ServiceBus;
using MyServiceBus.Serialization;

namespace MyServiceBus.AzureServiceBus;

internal static class AzureServiceBusMessageMapper
{
    private static readonly IMessageHeaderConvention HeaderConvention = MassTransitHeaderConvention.Instance;

    public static ServiceBusMessage CreateMessage(
        BinaryData body,
        IDictionary<string, object>? headers,
        string? contentType = null)
    {
        var message = new ServiceBusMessage(body)
        {
            ContentType = contentType ?? InboundMessageResolver.EnvelopeContentType
        };

        if (headers is null)
            return message;

        foreach (var (key, value) in headers)
        {
            if (key.StartsWith('_'))
            {
                ApplyNativeProperty(message, key[1..], value);
                continue;
            }

            if (TryNormalizeApplicationProperty(value, out var normalized))
                message.ApplicationProperties[key] = normalized;
        }

        return message;
    }

    public static ServiceBusMessage Copy(ServiceBusReceivedMessage received)
    {
        var copy = new ServiceBusMessage(received.Body)
        {
            ContentType = received.ContentType,
            CorrelationId = received.CorrelationId,
            MessageId = received.MessageId,
            ReplyTo = received.ReplyTo,
            ReplyToSessionId = received.ReplyToSessionId,
            SessionId = received.SessionId,
            Subject = received.Subject,
            To = received.To,
            TimeToLive = received.TimeToLive
        };

        foreach (var (key, value) in received.ApplicationProperties)
            copy.ApplicationProperties[key] = value;

        return copy;
    }

    public static Dictionary<string, object> CreateHeaders(ServiceBusReceivedMessage message, Uri? faultAddress)
    {
        var headers = message.ApplicationProperties.ToDictionary(x => x.Key, x => x.Value);
        headers[HeaderConvention.ContentTypeHeader] =
            message.ContentType ?? InboundMessageResolver.EnvelopeContentType;

        if (!string.IsNullOrWhiteSpace(message.MessageId))
            headers["message_id"] = message.MessageId;
        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
            headers["correlation_id"] = message.CorrelationId;
        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
            headers["reply_to"] = message.ReplyTo;
        if (faultAddress is not null && !headers.ContainsKey(HeaderConvention.FaultAddressHeader))
            headers[HeaderConvention.FaultAddressHeader] = faultAddress.ToString();

        return headers;
    }

    private static void ApplyNativeProperty(ServiceBusMessage message, string key, object? value)
    {
        var text = value?.ToString();
        switch (key)
        {
            case "content_type":
                message.ContentType = text;
                break;
            case "correlation_id":
                message.CorrelationId = text;
                break;
            case "message_id":
                message.MessageId = text;
                break;
            case "reply_to":
                message.ReplyTo = text;
                break;
            case "subject":
            case "type":
                message.Subject = text;
                break;
            case "to":
                message.To = text;
                break;
            default:
                if (TryNormalizeApplicationProperty(value, out var normalized))
                    message.ApplicationProperties[key] = normalized;
                break;
        }
    }

    private static bool TryNormalizeApplicationProperty(object? value, out object normalized)
    {
        switch (value)
        {
            case null:
                normalized = string.Empty;
                return true;
            case string or bool or byte or short or int or long or float or double or byte[]:
                normalized = value;
                return true;
            case Uri uri:
                normalized = uri.ToString();
                return true;
            case Enum enumValue:
                normalized = enumValue.ToString();
                return true;
            default:
                normalized = value.ToString() ?? string.Empty;
                return true;
        }
    }
}
