namespace MyServiceBus.Topology;

public class MessageTopology
{
    public Type MessageType { get; set; }
    public string EntityName { get; set; } // exchange or topic name
    public List<Type> ImplementedInterfaces { get; set; } = new();
}
