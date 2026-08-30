using Amazon.SimpleNotificationService;
using Amazon.SQS;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public sealed class AmazonSqsSendTransport : ISendTransport
{
    private readonly IAmazonSQS _sqs;
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly AmazonSqsEntityKind _kind;
    private readonly string _destination;
    private readonly string _entityName;

    internal AmazonSqsSendTransport(
        IAmazonSQS sqs,
        IAmazonSimpleNotificationService sns,
        AmazonSqsEntityKind kind,
        string destination,
        string entityName)
    {
        _sqs = sqs;
        _sns = sns;
        _kind = kind;
        _destination = destination;
        _entityName = entityName;
    }

    /// <exception cref="AmazonSqsTransportException">The AWS client rejected or could not complete the send.</exception>
    public async Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            var body = context.GetMessageBody(message).GetBytes();
            if (body.Length > 1_048_576)
                throw new AmazonSqsTransportException("send", _entityName,
                    new ArgumentOutOfRangeException(nameof(message), "Amazon SQS/SNS messages cannot exceed 1 MiB."));

            if (_kind == AmazonSqsEntityKind.Topic)
                await _sns.PublishAsync(AmazonSqsMessageMapper.CreateSnsRequest(
                    _destination, body, InboundMessageResolver.EnvelopeContentType), cancellationToken).ConfigureAwait(false);
            else
                await _sqs.SendMessageAsync(AmazonSqsMessageMapper.CreateSqsRequest(
                    _destination, body, InboundMessageResolver.EnvelopeContentType), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not AmazonSqsTransportException)
        {
            throw new AmazonSqsTransportException("send", _entityName, exception);
        }
    }
}
