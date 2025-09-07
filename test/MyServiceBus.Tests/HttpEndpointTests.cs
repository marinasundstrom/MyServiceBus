using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using System.Linq;

namespace MyServiceBus;

public class HttpEndpointTests
{
    [Fact]
    public async Task Send_posts_to_configured_uri()
    {
        var handler = new CaptureHandler();
        var client = new HttpClient(handler);
        var endpoint = new HttpEndpoint(client, new Uri("https://example.com/hook"));

        await endpoint.Send(new { Text = "hi" });

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.RequestUri.ShouldBe(new Uri("https://example.com/hook"));
    }

    [Fact]
    public async Task Send_applies_context_headers()
    {
        var handler = new CaptureHandler();
        var client = new HttpClient(handler);
        var endpoint = new HttpEndpoint(client, new Uri("https://example.com/hook"));

        await endpoint.Send(new { Text = "hi" }, ctx =>
        {
            if (ctx is HttpSendContext hctx)
            {
                hctx.ContentType = "text/plain";
                hctx.Headers["X-Test"] = "42";
            }
        });

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Content!.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        handler.LastRequest!.Headers.GetValues("X-Test").ShouldContain("42");
    }

    [Fact]
    public async Task ReadAsync_is_empty()
    {
        var handler = new CaptureHandler();
        var client = new HttpClient(handler);
        var endpoint = new HttpEndpoint(client, new Uri("https://example.com"));

        var enumerator = endpoint.ReadAsync().GetAsyncEnumerator();
        (await enumerator.MoveNextAsync()).ShouldBeFalse();
    }

    private class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
