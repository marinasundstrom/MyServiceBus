using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
