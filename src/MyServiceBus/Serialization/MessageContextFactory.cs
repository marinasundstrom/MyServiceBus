using MyServiceBus.Transports;
using System.Text.Json;

namespace MyServiceBus.Serialization;

// TODO: Message formatter

public class MessageContextFactory
{
    [Throws(typeof(InvalidOperationException))]
    public IMessageContext CreateMessageContext(ITransportMessage transportMessage)
    {
        if (transportMessage.Headers.TryGetValue("content_type", out var contentType))
        {
            switch (contentType.ToString())
            {
                case "application/vnd.mybus.envelope+json":
                    return new EnvelopeMessageContext(transportMessage.Payload, transportMessage.Headers);
                case "application/json":
                    return new RawJsonMessageContext(transportMessage.Payload, transportMessage.Headers);
                default:
                    throw new InvalidOperationException("Invalid Content Type");
            }
        }
        else
        {
            throw new InvalidOperationException("Header Content-Type was not found");
        }
    }
}
