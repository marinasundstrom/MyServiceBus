using System.Net.Http;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed class HttpTransportFactory : ITransportFactory
{
    private readonly HttpClient _client;
    private readonly IMessageSerializer _serializer;

    public HttpTransportFactory(HttpClient client, IMessageSerializer serializer)
    {
        _client = client;
        _serializer = serializer;
    }

    public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
    {
        var endpoint = new HttpEndpoint(_client, address);
        ISendTransport transport = new EndpointSendTransport(endpoint, _serializer);
        return Task.FromResult(transport);
    }

    public Task<IReceiveTransport> CreateReceiveTransport(
        ReceiveEndpointTopology topology,
        Func<ReceiveContext, Task> handler,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("HTTP transport does not support receiving messages");
    }
}
