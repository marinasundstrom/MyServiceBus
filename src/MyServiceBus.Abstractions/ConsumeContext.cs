
namespace MyServiceBus;

public interface ConsumeContext :
    PipeContext,
    MessageConsumeContext,
    IPublishEndpoint,
    ISendEndpointProvider
{

}

public interface ConsumeContext<out TMessage> : ConsumeContext
    where TMessage : class
{
    TMessage Message { get; }
}
