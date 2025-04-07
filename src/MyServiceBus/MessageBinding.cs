namespace MyServiceBus;

public class MessageBinding
{
    public Type MessageType { get; set; }
    public string EntityName { get; set; } // exchange to bind to
}
