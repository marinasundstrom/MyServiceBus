using System.Threading.Tasks;

namespace MyServiceBus;

public interface IConsumerFactory<TConsumer>
    where TConsumer : class
{
    Task Send<TMessage>(ConsumeContext<TMessage> context,
        IPipe<ConsumerConsumeContext<TConsumer, TMessage>> next)
        where TMessage : class;
}
