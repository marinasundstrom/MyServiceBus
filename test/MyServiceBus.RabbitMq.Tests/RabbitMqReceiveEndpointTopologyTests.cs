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
        projection.ExchangeName.ShouldBe("Contracts:OrderSubmitted");
        projection.ExchangeType.ShouldBe("fanout");
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
}
