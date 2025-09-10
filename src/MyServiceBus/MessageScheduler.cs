using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class MessageScheduler : IMessageScheduler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly IJobScheduler _jobScheduler;

    public MessageScheduler(IPublishEndpoint publishEndpoint, ISendEndpointProvider sendEndpointProvider, IJobScheduler jobScheduler)
    {
        _publishEndpoint = publishEndpoint;
        _sendEndpointProvider = sendEndpointProvider;
        _jobScheduler = jobScheduler;
    }

    public Task SchedulePublish<T>(T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
        => _jobScheduler.Schedule(scheduledTime, ct => _publishEndpoint.Publish(message, cancellationToken: ct), cancellationToken);

    public Task SchedulePublish<T>(T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
        => _jobScheduler.Schedule(delay, ct => _publishEndpoint.Publish(message, cancellationToken: ct), cancellationToken);

    public Task ScheduleSend<T>(Uri destination, T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
        => _jobScheduler.Schedule(scheduledTime, async ct =>
        {
            var endpoint = await _sendEndpointProvider.GetSendEndpoint(destination);
            await endpoint.Send(message, cancellationToken: ct);
        }, cancellationToken);

    public Task ScheduleSend<T>(Uri destination, T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
        => ScheduleSend(destination, message, DateTime.UtcNow + delay, cancellationToken);
}
