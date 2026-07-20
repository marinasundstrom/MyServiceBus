using MyServiceBus.Topology;
using Shouldly;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqReceiveEndpointTopologyTests
{
    [Fact]
    public void Projects_and_validates_receive_endpoint_intent()
    {
        var projection = RabbitMqReceiveEndpointTopology.Project(new ReceiveEndpointTopology
        {
            QueueName = "orders",
            ExchangeName = "Contracts:OrderSubmitted",
            RoutingKey = string.Empty,
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false,
            PrefetchCount = 16
        });

        projection.QueueName.ShouldBe("orders");
        projection.Bindings[0].ExchangeName.ShouldBe("Contracts:OrderSubmitted");
        projection.Bindings[0].ExchangeType.ShouldBe("fanout");
        projection.Durable.ShouldBeTrue();
        projection.AutoDelete.ShouldBeFalse();
        projection.PrefetchCount.ShouldBe((ushort)16);
    }

    [Fact]
    public void Rejects_conflicting_durability_intent()
    {
        var endpoint = new ReceiveEndpointTopology
        {
            QueueName = "orders",
            ExchangeName = "Contracts:OrderSubmitted",
            Durable = true,
            AutoDelete = true
        };

        Should.Throw<ArgumentException>(() => RabbitMqReceiveEndpointTopology.Project(endpoint));
    }

    [Fact]
    public void Projects_all_portable_bindings()
    {
        var endpoint = new ReceiveEndpointTransportTopology(
            "orders",
            durable: true,
            temporary: false,
            prefetchCount: 0,
            [
                new MessageBinding { MessageType = typeof(string), EntityName = "Contracts:OrderSubmitted" },
                new MessageBinding { MessageType = typeof(object), EntityName = "Contracts:OrderUpdated" }
            ]);

        var projection = RabbitMqReceiveEndpointTopology.Project(endpoint);

        projection.Bindings.Select(x => x.ExchangeName).ShouldBe([
            "Contracts:OrderSubmitted",
            "Contracts:OrderUpdated"
        ]);
    }
}
