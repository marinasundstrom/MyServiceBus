using System;
using System.Collections.Generic;

namespace MyServiceBus;

public interface ISendContext : PipeContext, IMessageScheduler
{
    string MessageId { get; set; }
    string RoutingKey { get; set; }
    IDictionary<string, object> Headers { get; }
    string? CorrelationId { get; set; }
    Uri? ResponseAddress { get; set; }
    Uri? FaultAddress { get; set; }
}
