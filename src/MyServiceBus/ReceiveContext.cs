using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public interface ReceiveContext : PipeContext
{
    Guid MessageId { get; }
    IList<string> MessageType { get; }
    Uri? ResponseAddress { get; }
    Uri? FaultAddress { get; }
    Uri? ErrorAddress { get; }

    IDictionary<string, object> Headers { get; }

    bool TryGetMessage<T>([NotNullWhen(true)] out T? message)
        where T : class;
}

public class ReceiveContextImpl : BasePipeContext, ReceiveContext
{
    private readonly IMessageContext messageContext;

    public ReceiveContextImpl(IMessageContext messageContext, Uri? errorAddress = null, CancellationToken cancellationToken = default)
        : base(cancellationToken)
    {
        this.messageContext = messageContext ?? throw new ArgumentNullException(nameof(messageContext));
        ErrorAddress = errorAddress;
    }

    public IDictionary<string, object> Headers => messageContext.Headers;

    public Guid MessageId => messageContext.MessageId;

    public IList<string> MessageType => messageContext.MessageType;

    public Uri? ResponseAddress => messageContext.ResponseAddress;

    public Uri? FaultAddress => messageContext.FaultAddress;

    public Uri? ErrorAddress { get; }

    public bool TryGetMessage<T>([NotNullWhen(true)] out T? message)
        where T : class
    {
        return messageContext.TryGetMessage(out message);
    }
}

