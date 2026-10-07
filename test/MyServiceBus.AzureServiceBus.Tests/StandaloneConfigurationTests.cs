using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus.AzureServiceBus.Tests;

public class StandaloneConfigurationTests
{
    [Fact]
    public async Task Standalone_resolution_applies_transport_configuration_once_before_constructing_clients()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddServiceBus(cfg => cfg.UsingAzureServiceBus((context, azure) =>
        {
            calls++;
            azure.Host("Endpoint=sb://configured.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZS1rZXk=");
        }));
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        Assert.Equal("configured.servicebus.windows.net", bus.Address.Host);
        await bus.StartAsync(CancellationToken.None);
        await bus.StopAsync(CancellationToken.None);
        Assert.Equal(1, calls);
    }
}
