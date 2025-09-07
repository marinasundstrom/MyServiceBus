using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class HttpEndpoint : IEndpoint
{
    private readonly HttpClient _client;
    private readonly Uri _uri;

    public HttpEndpoint(HttpClient client, Uri uri)
    {
        _client = client;
        _uri = uri;
    }

    public EndpointCapabilities Capabilities => EndpointCapabilities.None;

    public async Task Send<T>(T message, Action<ISendContext>? configure = null, CancellationToken cancellationToken = default)
    {
        var context = new HttpSendContext(MessageTypeCache.GetMessageTypes(typeof(T)), new EnvelopeMessageSerializer(), cancellationToken);
        configure?.Invoke(context);

        var json = JsonSerializer.Serialize(message);
        using var request = new HttpRequestMessage(HttpMethod.Post, _uri)
        {
            Content = new StringContent(json, Encoding.UTF8, context.ContentType)
        };

        foreach (var header in context.Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value?.ToString());

        await _client.SendAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<ReceiveContext> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public IDisposable Subscribe(Func<ReceiveContext, Task> handler) =>
        throw new NotSupportedException("HTTP endpoint is send-only");
}
