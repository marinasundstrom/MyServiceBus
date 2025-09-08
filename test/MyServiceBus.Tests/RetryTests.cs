using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus;
using Xunit;

public class RetryTests
{
    class TestMessage { }

    class FailingConsumer : IConsumer<TestMessage>
    {
        public static int Attempts;

        public Task Consume(ConsumeContext<TestMessage> context)
        {
            Attempts++;
            if (Attempts < 2)
                throw new InvalidOperationException("boom");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task AddConsumer_does_not_retry_by_default()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<FailingConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        FailingConsumer.Attempts = 0;
        var bus = provider.GetRequiredService<IMessageBus>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.Publish(new TestMessage()));
        Assert.Equal(1, FailingConsumer.Attempts);

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AddConsumer_retries_when_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(cfg =>
        {
            cfg.UsingMediator();
            cfg.AddConsumer<FailingConsumer, TestMessage>(c => c.UseRetry(2));
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        FailingConsumer.Attempts = 0;
        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.Publish(new TestMessage());
        Assert.Equal(2, FailingConsumer.Attempts);

        await hosted.StopAsync(CancellationToken.None);
    }
}
