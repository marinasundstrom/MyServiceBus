using System.Net;
using System.Net.Sockets;
using MyServiceBus.Topology;
using MyServiceBus;
using MyServiceBus.Serialization;
using Shouldly;

namespace MyServiceBus.Http.Tests;

public class HttpTransportTests
{
    [Throws(typeof(SocketException))]
    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(SocketException))]
    public async Task Sends_and_receives_message()
    {
        var port = GetFreePort();
        var address = new Uri($"http://localhost:{port}/input");
        var factory = new HttpTransportFactory();

        ReceiveContext? received = null;
        var transport = await factory.CreateReceiveTransport(new EndpointDefinition { Address = address.ToString() }, ctx =>
        {
            received = ctx;
            return Task.CompletedTask;
        });

        await transport.Start();
        try
        {
            var send = await factory.GetSendTransport(address);
            var serializer = new EnvelopeMessageSerializer();
            var sendCtx = new SendContextFactory().Create([typeof(string)], serializer);
            sendCtx.Headers["foo"] = "bar";
            await send.Send("hello", sendCtx);

            await Task.Delay(100);
            received.ShouldNotBeNull();
            received!.Headers["foo"].ShouldBe("bar");
        }
        finally
        {
            await transport.Stop();
        }
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(SocketException))]
    public async Task Sets_default_fault_address()
    {
        var port = GetFreePort();
        var address = new Uri($"http://localhost:{port}/input");
        var factory = new HttpTransportFactory();

        ReceiveContext? received = null;
        var transport = await factory.CreateReceiveTransport(
            new EndpointDefinition { Address = address.ToString(), ConfigureErrorEndpoint = true },
            ctx => { received = ctx; return Task.CompletedTask; });

        await transport.Start();
        try
        {
            var send = await factory.GetSendTransport(address);
            var serializer = new EnvelopeMessageSerializer();
            var sendCtx = new SendContextFactory().Create([typeof(string)], serializer);
            await send.Send("hi", sendCtx);

            await Task.Delay(100);
            received.ShouldNotBeNull();
            received!.Headers.ShouldContainKey(MessageHeaders.FaultAddress);
            received!.Headers[MessageHeaders.FaultAddress].ShouldBe($"http://localhost:{port}/input_fault");
        }
        finally
        {
            await transport.Stop();
        }
    }

    [Fact]
    [Throws(typeof(UriFormatException), typeof(SocketException))]
    public async Task Returns_error_when_handler_faults()
    {
        var port = GetFreePort();
        var address = new Uri($"http://localhost:{port}/input");
        var factory = new HttpTransportFactory();
        var transport = await factory.CreateReceiveTransport(new EndpointDefinition { Address = address.ToString() }, _ => throw new InvalidOperationException("boom"));

        await transport.Start();
        try
        {
            var send = await factory.GetSendTransport(address);
            var serializer = new EnvelopeMessageSerializer();
            var sendCtx = new SendContextFactory().Create([typeof(string)], serializer);
            await Should.ThrowAsync<HttpRequestException>(() => send.Send("hi", sendCtx));
        }
        finally
        {
            await transport.Stop();
        }
    }
}
