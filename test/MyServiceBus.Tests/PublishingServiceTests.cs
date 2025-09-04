using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using Xunit;
using Xunit.Sdk;

public class PublishingServiceTests
{
    record ValueSubmitted(Guid Value);

    class PublishingService
    {
        readonly IPublishEndpoint publishEndpoint;

        public PublishingService(IPublishEndpoint publishEndpoint) => this.publishEndpoint = publishEndpoint;

        public Task Submit(Guid value) => publishEndpoint.PublishAsync(new ValueSubmitted(value));
    }

    class PublishEndpointAdapter : IPublishEndpoint
    {
        readonly MyServiceBus.IMessageBus bus;

        public PublishEndpointAdapter(MyServiceBus.IMessageBus bus) => this.bus = bus;

        public Task PublishAsync<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
            => bus.Publish((dynamic)message, contextCallback, cancellationToken);

        public Task PublishAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
            => bus.Publish((dynamic)message!, contextCallback, cancellationToken);
    }

    [Fact]
    [Throws(typeof(InvalidOperationException))]
    public async Task Should_publish_message_from_service()
    {
        var services = new ServiceCollection();
        services.AddServiceBusTestHarness(_ => { });
        services.AddScoped<IPublishEndpoint, PublishEndpointAdapter>();
        services.AddScoped<PublishingService>();

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        harness.RegisterHandler<ValueSubmitted>(_ => Task.CompletedTask);

        await harness.Start();
        var service = provider.GetRequiredService<PublishingService>();
        await service.Submit(Guid.NewGuid());

        Assert.True(harness.WasConsumed<ValueSubmitted>());

        await harness.Stop();
    }
}
