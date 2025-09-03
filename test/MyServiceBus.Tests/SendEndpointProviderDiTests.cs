using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using Xunit;
using Xunit.Sdk;

public class SendEndpointProviderDiTests
{
    [Fact]
    [Throws(typeof(NotNullException), typeof(InvalidOperationException), typeof(UriFormatException))]
    public async Task Should_resolve_send_endpoint_provider()
    {
        var services = new ServiceCollection();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
        });

        using var provider = services.BuildServiceProvider();
        var sendEndpointProvider = provider.GetRequiredService<ISendEndpointProvider>();

        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("loopback://localhost/test"));

        Assert.NotNull(endpoint);
    }
}
