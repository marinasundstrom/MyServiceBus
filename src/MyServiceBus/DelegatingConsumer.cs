namespace MyServiceBus;

public class DelegatingConsumer : IMessageHandler
{
    private readonly Func<ReceiveContext, Task> _delegate;

    public DelegatingConsumer(Func<ReceiveContext, Task> @delegate)
    {
        _delegate = @delegate;
    }

    public Task Handle(ReceiveContext context) => _delegate(context);
}