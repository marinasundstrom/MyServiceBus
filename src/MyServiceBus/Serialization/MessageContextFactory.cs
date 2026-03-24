using MyServiceBus.Transports;

namespace MyServiceBus.Serialization;

public class MessageContextFactory : IInboundMessageResolver
{
    private readonly InboundMessageResolver _resolver = new();

    public IInboundMessage Resolve(ITransportMessage transportMessage)
        => _resolver.Resolve(transportMessage);

    public IMessageContext CreateMessageContext(ITransportMessage transportMessage)
        => (IMessageContext)Resolve(transportMessage);
}
