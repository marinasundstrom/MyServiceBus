using Amazon.SQS.Model;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public sealed class AmazonSqsReceiveContext : ReceiveContextImpl
{
    internal AmazonSqsReceiveContext(
        IInboundMessage messageContext,
        Message message,
        Uri? errorAddress,
        CancellationToken cancellationToken)
        : base(messageContext, errorAddress, cancellationToken)
    {
        Message = message;
    }

    public Message Message { get; }
}
