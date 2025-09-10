using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyServiceBus.Serialization;

namespace MyServiceBus;

internal class SendEndpointProvider : ISendEndpointProvider
{
    readonly ITransportFactory _transportFactory;
    readonly ISendPipe _sendPipe;
    readonly IMessageSerializer _serializer;
    readonly ConsumeContextProvider _contextProvider;
    readonly IMessageBus _bus;
    readonly ISendContextFactory _sendContextFactory;
    readonly ILoggerFactory? _loggerFactory;
    readonly IJobScheduler _jobScheduler;

    public SendEndpointProvider(ITransportFactory transportFactory, ISendPipe sendPipe, IMessageSerializer serializer,
        ConsumeContextProvider contextProvider, IMessageBus bus, ISendContextFactory sendContextFactory, ILoggerFactory? loggerFactory = null, IJobScheduler? jobScheduler = null)
    {
        _transportFactory = transportFactory;
        _sendPipe = sendPipe;
        _serializer = serializer;
        _contextProvider = contextProvider;
        _bus = bus;
        _sendContextFactory = sendContextFactory;
        _loggerFactory = loggerFactory;
        _jobScheduler = jobScheduler ?? new DefaultJobScheduler();
    }

    public Task<ISendEndpoint> GetSendEndpoint(Uri uri)
    {
        if (_contextProvider.Context != null)
            return _contextProvider.Context.GetSendEndpoint(uri);

        var logger = _loggerFactory?.CreateLogger<TransportSendEndpoint>();
        ISendEndpoint endpoint = new TransportSendEndpoint(_transportFactory, _sendPipe, _serializer, uri, _bus.Address, _sendContextFactory, logger, _jobScheduler);
        return Task.FromResult(endpoint);
    }
}
