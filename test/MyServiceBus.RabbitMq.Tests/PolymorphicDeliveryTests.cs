using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using Testcontainers.RabbitMq;

namespace MyServiceBus.RabbitMq.Tests;

[Collection(RabbitMqInteroperabilityCollection.Name)]
public class PolymorphicDeliveryTests
{
    [MassTransit.EntityName("acme-order")]
    [MassTransit.MessageUrn("Acme:Order")]
    [EntityName("acme-order")]
    [MessageUrn("urn:message:Acme:LocalOnly", false)]
    public interface IOrder { int Quantity { get; } }
    [MassTransit.EntityName("acme-base-order")]
    [MassTransit.MessageUrn("Acme:BaseOrder")]
    [EntityName("acme-base-order")]
    [MessageUrn("urn:message:Acme:BaseOrder", false)]
    public class BaseOrder { public int Quantity { get; set; } }
    [MassTransit.EntityName("acme-submitted-order")]
    [MassTransit.MessageUrn("Acme:SubmittedOrder")]
    [EntityName("acme-submitted-order")]
    [MessageUrn("urn:message:Acme:SubmittedOrder", false)]
    public class SubmittedOrder : BaseOrder, IOrder { }

    public sealed class InterfaceConsumer : IConsumer<IOrder>
    {
        public static TaskCompletionSource<int> Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Consume(ConsumeContext<IOrder> context) { Received.TrySetResult(context.Message.Quantity); return Task.CompletedTask; }
    }
    public sealed class BaseConsumer : IConsumer<BaseOrder>
    {
        public static TaskCompletionSource<int> Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Consume(ConsumeContext<BaseOrder> context) { Received.TrySetResult(context.Message.Quantity); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Publisher_only_bus_reaches_independent_base_and_interface_subscribers()
    {
        await using var broker = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
        await broker.StartAsync();
        InterfaceConsumer.Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        BaseConsumer.Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new Uri(broker.GetConnectionString());
        var credentials = connection.UserInfo.Split(':');
        ServiceProvider Build(Action<IBusRegistrationConfigurator> consumers)
        {
            var services = new ServiceCollection();
            services.AddServiceBus(cfg =>
            {
                consumers(cfg);
                cfg.SetMessageUrn<IOrder>("urn:message:Acme:Order");
                cfg.UsingRabbitMq((context, rabbit) =>
                {
                    rabbit.Host(connection.Host, connection.Port, host =>
                    {
                        host.Username(Uri.UnescapeDataString(credentials[0])); host.Password(Uri.UnescapeDataString(credentials[1]));
                    });
                    rabbit.ConfigureEndpoints(context);
                });
            });
            return services.BuildServiceProvider();
        }
        await using var interfaces = Build(cfg => cfg.AddConsumer<InterfaceConsumer>());
        await using var bases = Build(cfg => cfg.AddConsumer<BaseConsumer>());
        await using var publisher = Build(_ => { });
        var buses = new[] { interfaces, bases, publisher }.Select(p => p.GetRequiredService<IMessageBus>()).ToArray();
        try
        {
            foreach (var bus in buses) await bus.StartAsync(CancellationToken.None);
            await buses[2].Publish(new SubmittedOrder { Quantity = 7 });
            Assert.Equal(7, await InterfaceConsumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(7, await BaseConsumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(connection.Port, buses[2].Address.Port);
            using (var javaConsumer = JavaInteropPeer.Start(broker, "polymorphic-consume", "unused", "unused", "9"))
            {
                await JavaInteropPeer.WaitForOutput(javaConsumer, "READY", TimeSpan.FromSeconds(120));
                InterfaceConsumer.Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
                BaseConsumer.Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await buses[2].Publish(new SubmittedOrder { Quantity = 9 });
                Assert.Equal(9, await InterfaceConsumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(15)));
                Assert.Equal(9, await BaseConsumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(15)));
                await JavaInteropPeer.WaitForOutput(javaConsumer, "RECEIVED:9", TimeSpan.FromSeconds(30));
                await JavaInteropPeer.WaitForExit(javaConsumer, TimeSpan.FromSeconds(30));
            }
            InterfaceConsumer.Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
            BaseConsumer.Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var javaPublisher = JavaInteropPeer.Start(broker, "polymorphic-produce", "unused", "unused", "11"))
            {
                await JavaInteropPeer.WaitForExit(javaPublisher, TimeSpan.FromSeconds(120));
                Assert.Equal(11, await InterfaceConsumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(15)));
                Assert.Equal(11, await BaseConsumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(15)));
            }
            var massTransitReceived = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var massTransit = MassTransit.Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                cfg.Host(connection);
                cfg.ReceiveEndpoint("mass-transit-interface", endpoint =>
                {
                    endpoint.Handler<IOrder>(context =>
                    {
                        massTransitReceived.TrySetResult(context.Message.Quantity);
                        return Task.CompletedTask;
                    });
                });
            });
            await massTransit.StartAsync();
            try
            {
                InterfaceConsumer.Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
                BaseConsumer.Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await buses[2].Publish(new SubmittedOrder { Quantity = 13 });
                Assert.Equal(13, await massTransitReceived.Task.WaitAsync(TimeSpan.FromSeconds(15)));
                // Drain the local subscriptions before testing the reverse direction.
                Assert.Equal(13, await InterfaceConsumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(15)));
                Assert.Equal(13, await BaseConsumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(15)));
                InterfaceConsumer.Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
                BaseConsumer.Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await massTransit.Publish(new SubmittedOrder { Quantity = 17 });
                Assert.Equal(17, await InterfaceConsumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(15)));
                Assert.Equal(17, await BaseConsumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(15)));
            }
            finally { await massTransit.StopAsync(); }
        }
        finally
        {
            foreach (var bus in buses.Reverse()) await bus.StopAsync(CancellationToken.None);
        }
    }
}
