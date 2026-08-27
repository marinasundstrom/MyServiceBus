using Azure.Messaging.ServiceBus;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public sealed class AzureServiceBusReceiveContext : ReceiveContextImpl
{
    internal AzureServiceBusReceiveContext(
        IInboundMessage messageContext,
        ServiceBusReceivedMessage message,
        Uri? errorAddress,
        CancellationToken cancellationToken)
        : base(messageContext, errorAddress, cancellationToken)
    {
        Message = message;
    }

    public ServiceBusReceivedMessage Message { get; }
}
