using System;
using System.Text;
using MyServiceBus.Transports;

namespace MyServiceBus.Serialization;

public class InboundMessageResolver : IInboundMessageResolver
{
    public const string EnvelopeContentType = "application/vnd.masstransit+json";
    public const string RawJsonContentType = "application/json";
    private readonly IMessageHeaderConvention _headerConvention;

    public InboundMessageResolver(IMessageHeaderConvention? headerConvention = null)
    {
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
    }

    public IInboundMessage Resolve(ITransportMessage transportMessage)
    {
        var contentType = ReadContentType(transportMessage);
        return contentType switch
        {
            EnvelopeContentType => new EnvelopeMessageContext(transportMessage.Payload, transportMessage.Headers, _headerConvention),
            RawJsonContentType => new RawJsonMessageContext(transportMessage.Payload, transportMessage.Headers, _headerConvention),
            _ => throw new InvalidOperationException($"Invalid Content Type: {contentType}")
        };
    }

    private string ReadContentType(ITransportMessage transportMessage)
    {
        if (!transportMessage.Headers.TryGetValue(_headerConvention.ContentTypeHeader, out var contentTypeObj))
            return EnvelopeContentType;

        return contentTypeObj switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => contentTypeObj.ToString() ?? EnvelopeContentType
        };
    }
}
