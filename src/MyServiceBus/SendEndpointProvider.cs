using System;
using System.Threading.Tasks;
using MyServiceBus.Serialization;

namespace MyServiceBus;

internal class SendEndpointProvider : ISendEndpointProvider
{
    readonly ITransportFactory _transportFactory;
    readonly ISendPipe _sendPipe;
    readonly IMessageSerializer _serializer;
    readonly ConsumeContextProvider _contextProvider;
    readonly IMessageBus _bus;

    public SendEndpointProvider(ITransportFactory transportFactory, ISendPipe sendPipe, IMessageSerializer serializer,
        ConsumeContextProvider contextProvider, IMessageBus bus)
    {
        _transportFactory = transportFactory;
        _sendPipe = sendPipe;
        _serializer = serializer;
        _contextProvider = contextProvider;
        _bus = bus;
    }

    public Task<ISendEndpoint> GetSendEndpoint(Uri uri)
    {
        if (_contextProvider.Context != null)
            return _contextProvider.Context.GetSendEndpoint(uri);

        ISendEndpoint endpoint = new TransportSendEndpoint(_transportFactory, _sendPipe, _serializer, uri, _bus.Address);
        return Task.FromResult(endpoint);
    }
}
