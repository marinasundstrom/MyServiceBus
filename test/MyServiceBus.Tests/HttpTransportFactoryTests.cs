using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Serialization;
using Shouldly;

namespace MyServiceBus;

public class HttpTransportFactoryTests
{
    [Fact]
    public async Task Send_uses_http_endpoint()
    {
        var handler = new CaptureHandler();
        var client = new HttpClient(handler);
        var serializer = new EnvelopeMessageSerializer();
        var factory = new HttpTransportFactory(client, serializer);

        var transport = await factory.GetSendTransport(new Uri("https://example.com/hook"));
        var context = new SendContext(MessageTypeCache.GetMessageTypes(typeof(Message)), serializer)
        {
            DestinationAddress = new Uri("https://example.com/hook")
        };

        await transport.Send(new Message { Text = "hi" }, context);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.RequestUri.ShouldBe(new Uri("https://example.com/hook"));
    }

    private class Message
    {
        public string? Text { get; set; }
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
