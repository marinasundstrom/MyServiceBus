namespace MyServiceBus;

public interface IMessageBus
{
       Task StartAsync(CancellationToken cancellationToken);

       Task StopAsync(CancellationToken cancellationToken);

       Task Publish<T>(T message, string topic, CancellationToken cancellationToken = default)
              where T : class;
       Task AddConsumer<TMessage, TConsumer>(string queue, CancellationToken cancellationToken = default)
              where TConsumer : IConsumer<TMessage>
              where TMessage : class;
}