namespace MyServiceBus;

public sealed class HttpSendTransport : ISendTransport
{
    private readonly HttpClient _client;
    private readonly Uri _address;

    public HttpSendTransport(HttpClient client, Uri address)
    {
        _client = client;
        _address = address;
    }

    [Throws(typeof(HttpRequestException), typeof(TaskCanceledException), typeof(UriFormatException))]
    public async Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
        where T : class
    {
        var body = await context.Serialize(message);
        var request = new HttpRequestMessage(HttpMethod.Post, _address);
        request.Content = new ByteArrayContent(body.ToArray());

        if (context.Headers != null)
        {
            foreach (var header in context.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value?.ToString());
            }
        }

        if (request.Content.Headers.ContentType == null)
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
