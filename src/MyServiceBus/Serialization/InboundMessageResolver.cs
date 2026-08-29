using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MyServiceBus.Transports;

namespace MyServiceBus.Serialization;

public class InboundMessageResolver : IInboundMessageResolver
{
    public const string EnvelopeContentType = "application/vnd.masstransit+json";
    public const string RawJsonContentType = "application/json";
    private readonly IReadOnlyDictionary<string, IMessageDeserializer> _deserializers;
    private readonly IMessageDeserializer? _nServiceBusDeserializer;
    private readonly IMessageHeaderConvention _headerConvention;
    private readonly string _defaultContentType;

    public InboundMessageResolver(IMessageHeaderConvention? headerConvention = null)
        : this(
            [
                new EnvelopeMessageDeserializer(headerConvention ?? MassTransitHeaderConvention.Instance),
                new RawJsonMessageDeserializer(headerConvention ?? MassTransitHeaderConvention.Instance),
                new NServiceBusJsonMessageDeserializer()
            ],
            EnvelopeContentType,
            headerConvention)
    {
    }

    public InboundMessageResolver(
        IEnumerable<IMessageDeserializer> deserializers,
        string defaultContentType,
        IMessageHeaderConvention? headerConvention = null)
    {
        ArgumentNullException.ThrowIfNull(deserializers);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultContentType);
        _headerConvention = headerConvention ?? MassTransitHeaderConvention.Instance;
        _defaultContentType = defaultContentType;

        var configured = deserializers.ToArray();
        _nServiceBusDeserializer = configured.OfType<NServiceBusJsonMessageDeserializer>().LastOrDefault();
        _deserializers = configured
            .Where(deserializer => deserializer is not NServiceBusJsonMessageDeserializer)
            .GroupBy(deserializer => deserializer.ContentType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    public IInboundMessage Resolve(ITransportMessage transportMessage)
    {
        if (transportMessage.Headers.ContainsKey(NServiceBusHeaders.EnclosedMessageTypes)
            || transportMessage.Headers.ContainsKey(NServiceBusHeaders.ContentType))
        {
            if (_nServiceBusDeserializer is not null)
                return _nServiceBusDeserializer.Deserialize(
                    new ByteArrayMessageBody(transportMessage.Payload),
                    transportMessage.Headers);
        }

        var contentType = ReadContentType(transportMessage);
        if (!_deserializers.TryGetValue(contentType, out var deserializer))
            throw new InvalidOperationException($"Invalid Content Type: {contentType}");

        return deserializer.Deserialize(
            new ByteArrayMessageBody(transportMessage.Payload),
            transportMessage.Headers);
    }

    private string ReadContentType(ITransportMessage transportMessage)
    {
        if (!transportMessage.Headers.TryGetValue(_headerConvention.ContentTypeHeader, out var contentTypeObj))
            return _defaultContentType;

        return contentTypeObj switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => contentTypeObj.ToString() ?? _defaultContentType
        };
    }
}
