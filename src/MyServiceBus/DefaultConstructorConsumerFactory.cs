using System.Threading.Tasks;

namespace MyServiceBus;

public class DefaultConstructorConsumerFactory<TConsumer> : IConsumerFactory<TConsumer>
    where TConsumer : class, new()
{
    public Task Send<TMessage>(ConsumeContext<TMessage> context,
        IPipe<ConsumerConsumeContext<TConsumer, TMessage>> next) where TMessage : class
    {
        var consumer = new TConsumer();
        var consumerContext = new ConsumerConsumeContextImpl<TConsumer, TMessage>(consumer, context);
        return next.Send(consumerContext);
    }
}
