using MyServiceBus;

namespace TestApp;

public sealed class DashboardPublishMetricsFilter : IFilter<PublishContext>
{
    private readonly DashboardState _state;

    public DashboardPublishMetricsFilter(DashboardState state)
    {
        _state = state;
    }

    public Task Send(PublishContext context, IPipe<PublishContext> next)
    {
        _state.RecordPublished(
            context.DestinationAddress is null ? null : DashboardSnapshotFactory.ResolveMessageTypeFromDestination(context.DestinationAddress),
            context.DestinationAddress is null ? null : DashboardSnapshotFactory.ResolveMessageUrnFromDestination(context.DestinationAddress));
        return next.Send(context);
    }
}

public sealed class DashboardSendMetricsFilter : IFilter<SendContext>
{
    private readonly DashboardState _state;

    public DashboardSendMetricsFilter(DashboardState state)
    {
        _state = state;
    }

    public Task Send(SendContext context, IPipe<SendContext> next)
    {
        _state.RecordSent(
            context.DestinationAddress is null ? null : DashboardSnapshotFactory.ResolveMessageTypeFromDestination(context.DestinationAddress),
            context.DestinationAddress is null ? null : DashboardSnapshotFactory.ResolveMessageUrnFromDestination(context.DestinationAddress));
        return next.Send(context);
    }
}

public sealed class DashboardConsumeMetricsFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    private readonly DashboardState _state;
    private readonly string _queueName;

    public DashboardConsumeMetricsFilter(DashboardState state, string queueName)
    {
        _state = state;
        _queueName = queueName;
    }

    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        using var scope = _state.TrackConsume(_queueName, typeof(TMessage).FullName ?? typeof(TMessage).Name, MessageUrn.For(typeof(TMessage)));
        try
        {
            await next.Send(context);
            scope.MarkSuccess();
        }
        catch
        {
            scope.MarkFault();
            throw;
        }
    }
}
