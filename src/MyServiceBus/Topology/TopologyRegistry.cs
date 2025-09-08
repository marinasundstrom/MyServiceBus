using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MyServiceBus.Topology;

public class TopologyRegistry : IBusTopology
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

    [Throws(typeof(AmbiguousMatchException), typeof(TypeLoadException))]
    private MessageTopology RegisterMessage(Type messageType, string? entityName = null)
    {
        var messageTopology = new MessageTopology
        {
            MessageType = messageType,
            EntityName = entityName ?? EntityNameFormatter.Format(messageType)
        };
        Messages.Add(messageTopology);
        return messageTopology;
    }

    [Throws(typeof(AmbiguousMatchException))]
    public void RegisterConsumer<TConsumer>(string address, Delegate? configurePipe, params Type[] messageTypes)
    {
        var bindings = messageTypes.Select(mt =>
        {
            var msg = Messages.FirstOrDefault(m => m.MessageType == mt) ?? RegisterMessage(mt);
            return new MessageBinding { MessageType = mt, EntityName = msg.EntityName };
        }).ToList();

        Consumers.Add(new ConsumerTopology
        {
            ConsumerType = typeof(TConsumer),
            Address = address,
            Bindings = bindings,
            ConfigurePipe = configurePipe
        });
    }
}