using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using MyServiceBus.Serialization;
using SnsMessageAttributeValue = Amazon.SimpleNotificationService.Model.MessageAttributeValue;
using SqsMessageAttributeValue = Amazon.SQS.Model.MessageAttributeValue;

namespace MyServiceBus;

internal static class AmazonSqsMessageMapper
{
    private const string ContentTypeAttribute = "Content-Type";
    private static readonly IMessageHeaderConvention HeaderConvention = MassTransitHeaderConvention.Instance;

    public static SendMessageRequest CreateSqsRequest(string queueUrl, byte[] body, string contentType) =>
        new()
        {
            QueueUrl = queueUrl,
            MessageBody = System.Text.Encoding.UTF8.GetString(body),
            MessageAttributes = new Dictionary<string, SqsMessageAttributeValue>
            {
                [ContentTypeAttribute] = new() { DataType = "String", StringValue = contentType }
            }
        };

    public static PublishRequest CreateSnsRequest(string topicArn, byte[] body, string contentType) =>
        new()
        {
            TopicArn = topicArn,
            Message = System.Text.Encoding.UTF8.GetString(body),
            MessageAttributes = new Dictionary<string, SnsMessageAttributeValue>
            {
                [ContentTypeAttribute] = new() { DataType = "String", StringValue = contentType }
            }
        };

    public static Dictionary<string, object> CreateHeaders(Message message, Uri? faultAddress)
    {
        var headers = new Dictionary<string, object>(StringComparer.Ordinal);
        if (message.MessageAttributes.TryGetValue(ContentTypeAttribute, out var contentType) &&
            !string.IsNullOrWhiteSpace(contentType.StringValue))
            headers[HeaderConvention.ContentTypeHeader] = contentType.StringValue;
        else
            headers[HeaderConvention.ContentTypeHeader] = InboundMessageResolver.EnvelopeContentType;

        if (!string.IsNullOrWhiteSpace(message.MessageId))
            headers["message_id"] = message.MessageId;
        if (message.Attributes.TryGetValue("ApproximateReceiveCount", out var count))
            headers[MessageHeaders.RedeliveryCount] = Math.Max(0, int.Parse(count, System.Globalization.CultureInfo.InvariantCulture) - 1);
        if (faultAddress is not null && !headers.ContainsKey(HeaderConvention.FaultAddressHeader))
            headers[HeaderConvention.FaultAddressHeader] = faultAddress.ToString();
        return headers;
    }
}
