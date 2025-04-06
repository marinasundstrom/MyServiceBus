namespace MyServiceBus;

public interface IMessageHandler
{
    Task Handle(ReceiveContext context);
}
