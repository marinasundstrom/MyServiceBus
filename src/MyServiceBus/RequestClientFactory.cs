using MyServiceBus.Serialization;

namespace MyServiceBus;

public sealed class RequestClientFactory : IRequestClientFactory
{
    private readonly ITransportFactory _transportFactory;
    private readonly IMessageSerializer _serializer;
    private readonly ISendContextFactory _sendContextFactory;
    private readonly IBusHookDispatcher _hooks;
    private readonly MessageContractRegistry? _contracts;

    public RequestClientFactory(
        ITransportFactory transportFactory,
        IMessageSerializer serializer,
        ISendContextFactory sendContextFactory,
        IBusHookDispatcher hooks, MessageContractRegistry? contracts = null)
    {
        _transportFactory = transportFactory;
        _serializer = serializer;
        _sendContextFactory = sendContextFactory;
        _hooks = hooks;
        _contracts = contracts;
    }

    public IRequestClient<T> CreateRequestClient<T>(RequestTimeout timeout = default) where T : class
    {
        return new GenericRequestClient<T>(_transportFactory, _serializer, _sendContextFactory, timeout: timeout, hooks: _hooks, contracts: _contracts);
    }

    public IRequestClient<T> CreateRequestClient<T>(Uri destinationAddress, RequestTimeout timeout = default) where T : class
    {
        return new GenericRequestClient<T>(_transportFactory, _serializer, _sendContextFactory, destinationAddress, timeout, _hooks, _contracts);
    }
}
