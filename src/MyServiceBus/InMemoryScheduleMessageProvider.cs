namespace MyServiceBus;

public sealed class InMemoryScheduleMessageProvider : IScheduleMessageProvider
{
    private readonly IPublishEndpoint publishEndpoint;
    private readonly ISendEndpointProvider sendEndpointProvider;
    private readonly IJobScheduler jobScheduler;

    public InMemoryScheduleMessageProvider(
        IPublishEndpoint publishEndpoint,
        ISendEndpointProvider sendEndpointProvider,
        IJobScheduler jobScheduler)
    {
        this.publishEndpoint = publishEndpoint;
        this.sendEndpointProvider = sendEndpointProvider;
        this.jobScheduler = jobScheduler;
    }

    public ScheduleMessageProviderDurability Durability => ScheduleMessageProviderDurability.Volatile;

    public bool SupportsCancellation => true;

    public async Task<ScheduledMessageHandle> SchedulePublish<T>(
        DateTime scheduledTime,
        T message,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var tokenId = await jobScheduler.Schedule(
            scheduledTime,
            ct => publishEndpoint.Publish(message, cancellationToken: ct),
            cancellationToken);
        return new ScheduledMessageHandle(tokenId, scheduledTime);
    }

    public async Task<ScheduledMessageHandle> ScheduleSend<T>(
        Uri destinationAddress,
        DateTime scheduledTime,
        T message,
        CancellationToken cancellationToken = default)
        where T : class
    {
        async Task Callback(CancellationToken callbackCancellationToken)
        {
            var endpoint = await sendEndpointProvider.GetSendEndpoint(destinationAddress);
            await endpoint.Send(message, cancellationToken: callbackCancellationToken);
        }

        var tokenId = await jobScheduler.Schedule(scheduledTime, Callback, cancellationToken);
        return new ScheduledMessageHandle(tokenId, scheduledTime);
    }

    public Task Cancel(Guid tokenId, CancellationToken cancellationToken = default) => jobScheduler.Cancel(tokenId);
}
