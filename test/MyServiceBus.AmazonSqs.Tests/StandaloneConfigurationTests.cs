using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus.AmazonSqs.Tests;

public class StandaloneConfigurationTests
{
    [Fact]
    public async Task Standalone_resolution_applies_transport_configuration_once_before_constructing_clients()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddServiceBus(cfg => cfg.UsingAmazonSqs((context, azure) =>
        {
            calls++;
            azure.LocalstackHost("http://localhost:4566", "eu-west-1");
        }));
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        Assert.Equal("eu-west-1", bus.Address.Host);
        await bus.StartAsync(CancellationToken.None);
        await bus.StopAsync(CancellationToken.None);
        Assert.Equal(1, calls);
    }
}
