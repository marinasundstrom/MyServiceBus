using MyServiceBus.Serialization;

namespace MyServiceBus;

public sealed class RequestClientFactory : IScopedClientFactory
{
    private readonly ITransportFactory _transportFactory;
    private readonly IMessageSerializer _serializer;
    private readonly ISendContextFactory _sendContextFactory;

    public RequestClientFactory(ITransportFactory transportFactory, IMessageSerializer serializer, ISendContextFactory sendContextFactory)
    {
        _transportFactory = transportFactory;
        _serializer = serializer;
        _sendContextFactory = sendContextFactory;
    }

    public IRequestClient<T> CreateRequestClient<T>(RequestTimeout timeout = default) where T : class
    {
        return new GenericRequestClient<T>(_transportFactory, _serializer, _sendContextFactory, timeout: timeout);
    }

    public IRequestClient<T> CreateRequestClient<T>(Uri destinationAddress, RequestTimeout timeout = default) where T : class
    {
        return new GenericRequestClient<T>(_transportFactory, _serializer, _sendContextFactory, destinationAddress, timeout);
    }
}

