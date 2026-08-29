using Azure.Messaging.ServiceBus;
using MyServiceBus.AzureServiceBus;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public sealed class AzureServiceBusSendTransport : ISendTransport
{
    private readonly ServiceBusSender _sender;
    private readonly string _entityName;

    internal AzureServiceBusSendTransport(ServiceBusSender sender, string entityName)
    {
        _sender = sender;
        _entityName = entityName;
    }

    /// <exception cref="AzureServiceBusTransportException">
    /// The Azure Service Bus client rejected or could not complete the send operation.
    /// </exception>
    public async Task Send<T>(
        T message,
        SendContext context,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var body = context.GetMessageBody(message).GetBytes();
            var serviceBusMessage = AzureServiceBusMessageMapper.CreateMessage(
                BinaryData.FromBytes(body),
                context.Headers,
                InboundMessageResolver.EnvelopeContentType);

            if (!string.IsNullOrWhiteSpace(context.MessageId))
                serviceBusMessage.MessageId = context.MessageId;
            if (!string.IsNullOrWhiteSpace(context.CorrelationId))
                serviceBusMessage.CorrelationId = context.CorrelationId;
            if (context.ResponseAddress is not null)
                serviceBusMessage.ReplyTo = context.ResponseAddress.ToString();

            await _sender.SendMessageAsync(serviceBusMessage, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not AzureServiceBusTransportException)
        {
            throw new AzureServiceBusTransportException("send", _entityName, exception);
        }
    }
}
