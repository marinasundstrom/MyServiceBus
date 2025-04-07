namespace MyServiceBus.Topology;

public class TopologyRegistry
{
    public List<MessageTopology> Messages { get; } = new();
    public List<ConsumerTopology> Consumers { get; } = new();

    public void RegisterMessage<T>(string entityName)
    {
        Messages.Add(new MessageTopology
        {
            MessageType = typeof(T),
            EntityName = entityName
        });
    }

    private MessageTopology RegisterMessage(Type messageType, string? entityName = null)
    {
        var messageTopology = new MessageTopology
        {
            MessageType = messageType,
            EntityName = entityName ?? messageType.FullName
        };
        Messages.Add(messageTopology);
        return messageTopology;
    }

    public void RegisterConsumer<TConsumer>(string queueName, params Type[] messageTypes)
    {
        var bindings = messageTypes.Select(mt =>
        {
            var msg = Messages.FirstOrDefault(m => m.MessageType == mt) ?? RegisterMessage(mt);
            return new MessageBinding { MessageType = mt, EntityName = msg.EntityName };
        }).ToList();

        Consumers.Add(new ConsumerTopology
        {
            ConsumerType = typeof(TConsumer),
            QueueName = queueName,
            Bindings = bindings
        });
    }
}