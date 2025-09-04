using MyServiceBus.Serialization;

namespace MyServiceBus;

public sealed class RequestClientFactory : IScopedClientFactory
{
    private readonly ITransportFactory _transportFactory;
    private readonly IMessageSerializer _serializer;

    public RequestClientFactory(ITransportFactory transportFactory, IMessageSerializer serializer)
    {
        _transportFactory = transportFactory;
        _serializer = serializer;
    }

    public IRequestClient<T> CreateRequestClient<T>(RequestTimeout timeout = default) where T : class
    {
        return new GenericRequestClient<T>(_transportFactory, _serializer, timeout: timeout);
    }

    public IRequestClient<T> CreateRequestClient<T>(Uri destinationAddress, RequestTimeout timeout = default) where T : class
    {
        return new GenericRequestClient<T>(_transportFactory, _serializer, destinationAddress, timeout);
    }
}

