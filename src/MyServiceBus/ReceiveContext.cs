using System.Text.Json;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public interface ReceiveContext
{
    public Guid MessageId { get; }
    public IList<string> MessageType { get; }
    bool TryGetMessage<T>(out T? message)
        where T : class;
}

public class ReceiveContextImpl : ReceiveContext
{
    private IMessageContext messageContext;

    public ReceiveContextImpl(IMessageContext messageContext)
    {
        this.messageContext = messageContext ?? throw new ArgumentNullException(nameof(messageContext));
    }

    public IDictionary<string, object>? Headers { get; }

    public Guid MessageId => messageContext.MessageId;

    public IList<string> MessageType => messageContext.MessageType;

    public bool TryGetMessage<T>(out T? message)
        where T : class
    {
        return messageContext.TryGetMessage(out message);
    }
}