using MyServiceBus.Serialization;

namespace MyServiceBus;

internal sealed class EndpointSendTransport : ISendTransport
{
    private readonly IEndpoint _endpoint;
    private readonly IMessageSerializer _serializer;

    public EndpointSendTransport(IEndpoint endpoint, IMessageSerializer serializer)
    {
        _endpoint = endpoint;
        _serializer = serializer;
    }

    public async Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
        where T : class
    {
        // Serialize message to ensure filters have run; body isn't used yet
        _ = await context.Serialize(message);
        await _endpoint.Send(message, endpointCtx =>
        {
            endpointCtx.MessageId = context.MessageId;
            endpointCtx.CorrelationId = context.CorrelationId;
            endpointCtx.ResponseAddress = context.ResponseAddress;
            endpointCtx.FaultAddress = context.FaultAddress;
            foreach (var kv in context.Headers)
                endpointCtx.Headers[kv.Key] = kv.Value;
        }, cancellationToken);
    }
}
