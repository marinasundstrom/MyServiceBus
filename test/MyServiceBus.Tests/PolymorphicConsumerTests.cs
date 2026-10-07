using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus.Tests;

public class PolymorphicConsumerTests
{
    public interface IStatus { int Value { get; } }
    public record Status(int Value) : IStatus;
    public sealed class StatusConsumer : IConsumer<Status>, IConsumer<IStatus>
    {
        public static int Concrete;
        public static int Interface;
        public Task Consume(ConsumeContext<Status> context) { Concrete++; return Task.CompletedTask; }
        public Task Consume(ConsumeContext<IStatus> context) { Interface++; return Task.CompletedTask; }
    }

    [Fact]
    public async Task One_consumer_handles_the_most_specific_advertised_contract_once()
    {
        StatusConsumer.Concrete = StatusConsumer.Interface = 0;
        var services = new ServiceCollection();
        services.AddServiceBus(cfg => { cfg.AddConsumer<StatusConsumer>(); cfg.UsingMediator(); });
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.StartAsync(CancellationToken.None);
        await bus.Publish(new Status(1));
        Assert.Equal(1, StatusConsumer.Concrete);
        Assert.Equal(0, StatusConsumer.Interface);
        await bus.Publish<IStatus>(new Status(2));
        Assert.Equal(1, StatusConsumer.Concrete);
        Assert.Equal(1, StatusConsumer.Interface);
        await bus.StopAsync(CancellationToken.None);
    }
}
