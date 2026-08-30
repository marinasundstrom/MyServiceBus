using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus;
using MyServiceBus.Topology;

var unionType = typeof(OrderCommand);

Console.WriteLine($"Union: {unionType}");
foreach (var attribute in unionType.GetCustomAttributesData())
    Console.WriteLine($"Attribute: {attribute.AttributeType}");
foreach (var implementedInterface in unionType.GetInterfaces())
    Console.WriteLine($"Interface: {implementedInterface}");
foreach (var constructor in unionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
    Console.WriteLine($"Constructor: {constructor}");
foreach (var property in unionType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    Console.WriteLine($"Property: {property.PropertyType} {property.Name}");
foreach (var method in unionType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    Console.WriteLine($"Method: {method}");

OrderCommand submit = new SubmitOrder("A-42");
OrderCommand cancel = new CancelOrder("A-42");

Console.WriteLine($"Submit JSON: {JsonSerializer.Serialize(submit)}");
Console.WriteLine($"Cancel JSON: {JsonSerializer.Serialize(cancel)}");
try
{
    JsonSerializer.Deserialize<OrderCommand>(JsonSerializer.Serialize(submit));
    throw new InvalidOperationException("Object-shaped union cases should require an explicit JSON classifier.");
}
catch (JsonException)
{
}

var services = new ServiceCollection();
services.AddSingleton<UnionConsumerAudit>();
services.AddServiceBus(configurator =>
{
    configurator.UsingMediator();
    configurator.AddConsumerMethods(typeof(OrderConsumers));
});

await using var provider = services.BuildServiceProvider();
var topology = provider.GetRequiredService<TopologyRegistry>();
Assert(topology.Consumers.Count == 2, "The union consumer should expand into two case registrations.");
Assert(topology.ReceiveEndpoints.Count == 1, "Both cases should share one receive endpoint.");
Assert(topology.Messages.Any(message => message.MessageType == typeof(SubmitOrder)), "SubmitOrder topology is missing.");
Assert(topology.Messages.Any(message => message.MessageType == typeof(CancelOrder)), "CancelOrder topology is missing.");
Assert(topology.Messages.All(message => message.MessageType != typeof(OrderCommand)), "The union carrier leaked into message topology.");

var hostedService = provider.GetRequiredService<IHostedService>();
await hostedService.StartAsync(CancellationToken.None);
try
{
    var mediator = provider.GetRequiredService<IMediator>();
    await mediator.Send(new SubmitOrder("A-42"));
    await mediator.Send(new CancelOrder("A-42"));

    var audit = provider.GetRequiredService<UnionConsumerAudit>();
    Assert(audit.Events.SequenceEqual(["submit:A-42", "cancel:A-42"]), "The active union cases were not dispatched correctly.");
}
finally
{
    await hostedService.StopAsync(CancellationToken.None);
}

Response<OrderAccepted, OrderRejected> accepted = new OrderAccepted("A-42");
if (!accepted.TryGetValue(out OrderAccepted? acceptedMessage))
    throw new InvalidOperationException("The selected response case should be available.");
Assert(acceptedMessage.OrderId == "A-42", "The response case payload was changed.");
Assert(!accepted.TryGetValue(out OrderRejected? _), "The unselected response case should not be available.");
var matchedOrderId = accepted switch
{
    OrderAccepted response => response.OrderId,
    OrderRejected response => response.OrderId
};
Assert(matchedOrderId == "A-42", "The response union did not support exhaustive case matching.");
Assert(JsonSerializer.Serialize(accepted) == "{\"OrderId\":\"A-42\"}", "STJ should serialize only the selected response case.");

Console.WriteLine("Union consumer and response prototype passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

public sealed record SubmitOrder(string OrderId);

public sealed record CancelOrder(string OrderId);

public sealed record OrderAccepted(string OrderId);

public sealed record OrderRejected(string OrderId, string Reason);

public union OrderCommand(SubmitOrder, CancelOrder);

public sealed class UnionConsumerAudit
{
    public List<string> Events { get; } = [];
}

public static class OrderConsumers
{
    [Consumer("orders")]
    public static void Consume(OrderCommand command, UnionConsumerAudit audit)
    {
        switch (command)
        {
            case SubmitOrder submit:
                audit.Events.Add($"submit:{submit.OrderId}");
                break;
            case CancelOrder cancel:
                audit.Events.Add($"cancel:{cancel.OrderId}");
                break;
        }
    }
}
