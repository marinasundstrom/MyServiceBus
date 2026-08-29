using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using MyServiceBus.Persistence;

public sealed class BusOutboxTests
{
    [Fact]
    public async Task Scoped_publish_and_send_are_captured_by_active_outbox_session()
    {
        var publishCorrelationId = Guid.NewGuid();
        var sendCorrelationId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddServiceBus(configurator =>
        {
            configurator.UseBusOutbox();
            configurator.UsingMediator();
        });

        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var writer = new RecordingOutboxWriter();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var endpointProvider = scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>();
        var sendEndpoint = await endpointProvider.GetSendEndpoint(new Uri("loopback://localhost/orders"));
        using (scope.ServiceProvider.GetRequiredService<OutboxSession>().Begin(writer))
        {
            await bus.Publish(new DirectBusMessage(Guid.NewGuid()));

            await publishEndpoint.Publish(new OrderSubmitted(Guid.NewGuid()), context =>
                context.CorrelationId = publishCorrelationId.ToString());

            await sendEndpoint.Send(new SubmitOrder(Guid.NewGuid()), context =>
                context.CorrelationId = sendCorrelationId.ToString());
        }
        await publishEndpoint.Publish(new DirectBusMessage(Guid.NewGuid()));

        Assert.Collection(
            writer.Messages,
            published =>
            {
                Assert.Equal(OutboxDeliveryIntent.Publish, published.Intent);
                Assert.Equal(publishCorrelationId, published.CorrelationId);
            },
            sent =>
            {
                Assert.Equal(OutboxDeliveryIntent.Send, sent.Intent);
                Assert.Equal(sendCorrelationId, sent.CorrelationId);
                Assert.Equal(new Uri("loopback://localhost/orders"), sent.DestinationAddress);
            });

        await bus.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Nested_outbox_sessions_are_rejected()
    {
        var session = new OutboxSession();
        using var registration = session.Begin(new RecordingOutboxWriter());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            session.Begin(new RecordingOutboxWriter()));

        Assert.Contains("already active", exception.Message);
    }

    [Fact]
    public async Task Scheduled_messages_are_rejected_while_outbox_session_is_active()
    {
        var services = new ServiceCollection();
        services.AddServiceBus(configurator =>
        {
            configurator.UseBusOutbox();
            configurator.UsingMediator();
        });

        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        using var registration = scope.ServiceProvider.GetRequiredService<OutboxSession>()
            .Begin(new RecordingOutboxWriter());
        var endpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            endpoint.Publish(new OrderSubmitted(Guid.NewGuid()), context =>
                context.SetScheduledEnqueueTime(TimeSpan.FromMinutes(1))));

        Assert.Contains("transactional outbox", exception.Message);
        await bus.StopAsync(CancellationToken.None);
    }

    private sealed class RecordingOutboxWriter : IOutboxWriter
    {
        public List<OutboxMessage> Messages { get; } = [];

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed record OrderSubmitted(Guid OrderId);

    private sealed record SubmitOrder(Guid OrderId);

    private sealed record DirectBusMessage(Guid Id);
}
