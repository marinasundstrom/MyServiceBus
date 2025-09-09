using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MyServiceBus;
using Shouldly;

namespace MyServiceBus.Http.Tests;

public class HttpFactoryConfiguratorTests
{
    [Fact]
    [Throws(typeof(UriFormatException), typeof(SocketException))]
    public async Task Creates_and_starts_bus()
    {
        var port = GetFreePort();
        var bus = MessageBus.Factory.Create<HttpFactoryConfigurator>(cfg =>
        {
            cfg.Host(new Uri($"http://localhost:{port}/"));
        });

        bus.ShouldNotBeNull();
        await bus.StartAsync(CancellationToken.None);
        await bus.StopAsync(CancellationToken.None);
    }

    [Throws(typeof(SocketException))]
    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
