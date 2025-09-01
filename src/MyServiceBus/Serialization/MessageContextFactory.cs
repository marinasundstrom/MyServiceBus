using System;
using System.Text.Json;
using MyServiceBus.Transports;

namespace MyServiceBus.Serialization;

public class MessageContextFactory
{
    [Throws(typeof(InvalidOperationException))]
    public IMessageContext CreateMessageContext(ITransportMessage transportMessage)
    {
        if (transportMessage.Headers.TryGetValue("content_type", out var contentTypeObj))
        {
            var contentType = contentTypeObj.ToString();

            if (string.Equals(contentType, "application/vnd.mybus.envelope+json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/vnd.masstransit+json", StringComparison.OrdinalIgnoreCase))
            {
                return new EnvelopeMessageContext(transportMessage.Payload, transportMessage.Headers);
            }

            if (string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                return new RawJsonMessageContext(transportMessage.Payload, transportMessage.Headers);
            }

            throw new InvalidOperationException($"Invalid Content Type: {contentType}");
        }

        throw new InvalidOperationException("Header Content-Type was not found");
    }
}

