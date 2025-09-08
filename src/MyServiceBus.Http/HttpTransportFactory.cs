using System.Net;
using System.Collections.Concurrent;
using MyServiceBus.Topology;

namespace MyServiceBus;

public sealed class HttpTransportFactory : ITransportFactory
{
    private readonly ConcurrentDictionary<string, HttpClient> _clients = new();
    private readonly ConcurrentDictionary<Uri, ISendTransport> _sendTransports = new();

    [Throws(typeof(InvalidOperationException), typeof(UriFormatException))]
    public Task<ISendTransport> GetSendTransport(Uri address, CancellationToken cancellationToken = default)
    {
        var baseUri = new Uri(address.GetLeftPart(UriPartial.Authority));
        var client = _clients.GetOrAdd(baseUri.ToString(), _ => new HttpClient { BaseAddress = baseUri });

        var transport = _sendTransports.GetOrAdd(address, uri => new HttpSendTransport(client, uri));
        return Task.FromResult(transport);
    }

    [Throws(typeof(UriFormatException), typeof(PlatformNotSupportedException))]
    public Task<IReceiveTransport> CreateReceiveTransport(
        EndpointDefinition definition,
        Func<ReceiveContext, Task> handler,
        Func<string?, bool>? isMessageTypeRegistered = null,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(definition.Address, UriKind.Absolute);
        var listener = new HttpListener();
        var prefix = $"{uri.Scheme}://{uri.Authority}/";
        listener.Prefixes.Add(prefix);

        var transport = new HttpReceiveTransport(
            listener,
            uri.AbsolutePath.Trim('/'),
            handler,
            definition.ConfigureErrorEndpoint,
            isMessageTypeRegistered);

        return Task.FromResult<IReceiveTransport>(transport);
    }
}
