using System;
using System.Threading.Tasks;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class SendEndpointProvider : ISendEndpointProvider
{
    readonly ITransportFactory _transportFactory;
    readonly ISendPipe _sendPipe;
    readonly IMessageSerializer _serializer;
    readonly ConsumeContextProvider _contextProvider;

    public SendEndpointProvider(ITransportFactory transportFactory, ISendPipe sendPipe, IMessageSerializer serializer,
        ConsumeContextProvider contextProvider)
    {
        _transportFactory = transportFactory;
        _sendPipe = sendPipe;
        _serializer = serializer;
        _contextProvider = contextProvider;
    }

    public Task<ISendEndpoint> GetSendEndpoint(Uri uri)
    {
        if (_contextProvider.Context != null)
            return _contextProvider.Context.GetSendEndpoint(uri);

        ISendEndpoint endpoint = new TransportSendEndpoint(_transportFactory, _sendPipe, _serializer, uri);
        return Task.FromResult(endpoint);
    }
}
