using System;
using System.Collections.Generic;

namespace MyServiceBus.Topology;

public class ConsumerTopology
{
    public ConsumerDefinitionModel Definition { get; set; } = null!;
    public Type ConsumerType { get; set; }
    public string QueueName { get; set; }
    public bool EndpointNameIsExplicit { get; set; }
    public Type? EndpointNameFormatterType { get; set; }
    public List<MessageBinding> Bindings { get; set; } = new();
    public Delegate? ConfigurePipe { get; set; }
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public IConsumerRegistrationDescriptor? Registration { get; set; }
    public ushort? PrefetchCount { get; set; }
    public int? ConcurrentMessageLimit { get; set; }
    public IDictionary<string, object?>? QueueArguments { get; set; }
    public Type? SerializerType { get; set; }

    public string ResolveEndpointName(IEndpointNameFormatter? formatter)
        => !EndpointNameIsExplicit && EndpointNameFormatterType is not null && formatter is not null
            ? formatter.Format(EndpointNameFormatterType)
            : QueueName;
}
