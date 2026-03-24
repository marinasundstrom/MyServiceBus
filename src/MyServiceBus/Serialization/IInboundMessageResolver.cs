using MyServiceBus.Transports;

namespace MyServiceBus.Serialization;

public interface IInboundMessageResolver
{
    IInboundMessage Resolve(ITransportMessage transportMessage);
}
