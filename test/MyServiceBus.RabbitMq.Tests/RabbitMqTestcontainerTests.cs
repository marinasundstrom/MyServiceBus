using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqTestcontainerTests
{
    [Fact]
    public async Task Deferred_host_configuration_is_used_after_transport_resolution()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
        await container.StartAsync();

        var port = container.GetMappedPublicPort(5672);
        Assert.NotEqual(5672, port);
        var connectionUri = new Uri(container.GetConnectionString());
        var credentials = connectionUri.UserInfo.Split(':', 2);
        var queueName = $"deferred-configuration-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceBus(configurator =>
        {
            configurator.AddConsumer<DeferredConfigurationConsumer>();
            configurator.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(container.Hostname, port, host =>
                {
                    host.Username(Uri.UnescapeDataString(credentials[0]));
                    host.Password(Uri.UnescapeDataString(credentials[1]));
                });
                rabbit.ReceiveEndpoint(queueName, endpoint =>
                    endpoint.ConfigureConsumer<DeferredConfigurationConsumer>(context));
            });
        });

        await using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<ITransportFactory>();
        var hostedService = provider.GetServices<IHostedService>().OfType<ServiceBusHostedService>().Single();

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Transport_round_trips_an_envelope_through_rabbitmq()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
        await container.StartAsync();

        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(container.GetConnectionString())
        };
        var configurator = new RabbitMqFactoryConfigurator();
        var transportFactory = new RabbitMqTransportFactory(
            new ConnectionProvider(connectionFactory),
            configurator);

        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"compatibility-message-{suffix}";
        var queueName = $"compatibility-message-{suffix}";
        var expectedUrn = MessageUrn.For(typeof(CompatibilityMessage));
        var received = new TaskCompletionSource<CompatibilityMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var receiveTransport = await transportFactory.CreateReceiveTransport(
            new ReceiveEndpointTopology
            {
                QueueName = queueName,
                ExchangeName = exchangeName,
                Durable = false,
                AutoDelete = true
            },
            context =>
            {
                if (context.TryGetMessage<CompatibilityMessage>(out var message))
                    received.TrySetResult(message);

                return Task.CompletedTask;
            },
            messageType => messageType == expectedUrn);

        await receiveTransport.Start();
        try
        {
            var serializer = new EnvelopeMessageSerializer();
            var sendContext = new RabbitMqSendContext([typeof(CompatibilityMessage)], serializer)
            {
                DestinationAddress = new Uri($"rabbitmq://localhost/exchange/{exchangeName}")
            };
            var sendTransport = await transportFactory.GetSendTransport(
                new Uri($"exchange:{exchangeName}?durable=false&autodelete=true"));

            await sendTransport.Send(
                new CompatibilityMessage { Value = "from-dotnet" },
                sendContext);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("from-dotnet", message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    [Fact]
    public async Task Forced_stop_redelivers_unfinished_delivery_with_same_identity()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
        await container.StartAsync();

        var transportFactory = CreateTransportFactory(container);
        var replacementFactory = CreateTransportFactory(container);
        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"forced-stop-{suffix}";
        var queueName = exchangeName;
        var expectedUrn = MessageUrn.For(typeof(CompatibilityMessage));
        var firstStarted = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var redelivered = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var topology = new ReceiveEndpointTopology
        {
            QueueName = queueName,
            ExchangeName = exchangeName,
            Durable = false,
            AutoDelete = false,
            PrefetchCount = 1,
            ConcurrentMessageLimit = 1
        };

        var first = await transportFactory.CreateReceiveTransport(
            topology,
            async context =>
            {
                firstStarted.TrySetResult(context.MessageId);
                await releaseFirst.Task;
            },
            messageType => messageType == expectedUrn);

        await first.Start();
        IReceiveTransport? second = null;
        try
        {
            var serializer = new EnvelopeMessageSerializer();
            var sendContext = new RabbitMqSendContext([typeof(CompatibilityMessage)], serializer)
            {
                DestinationAddress = new Uri($"rabbitmq://localhost/exchange/{exchangeName}"),
                MessageId = Guid.NewGuid().ToString()
            };
            var sendTransport = await transportFactory.GetSendTransport(
                new Uri($"exchange:{exchangeName}?durable=false&autodelete=false"));

            await sendTransport.Send(new CompatibilityMessage { Value = "unfinished" }, sendContext);
            var originalMessageId = await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            using var stopSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.Stop(stopSource.Token));

            second = await replacementFactory.CreateReceiveTransport(
                topology,
                context =>
                {
                    redelivered.TrySetResult(context.MessageId);
                    return Task.CompletedTask;
                },
                messageType => messageType == expectedUrn);
            await second.Start();

            var redeliveredMessageId = await redelivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(originalMessageId, redeliveredMessageId);
        }
        finally
        {
            releaseFirst.TrySetResult();
            if (second != null)
                await second.Stop();
        }
    }

    [Fact]
    public async Task Prefetch_and_concurrency_bound_saturated_receiver()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1.8-alpine").Build();
        await container.StartAsync();

        var transportFactory = CreateTransportFactory(container);
        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"saturation-{suffix}";
        var queueName = exchangeName;
        var expectedUrn = MessageUrn.For(typeof(CompatibilityMessage));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var twoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stateSync = new object();
        var activeHandlers = 0;
        var maximumActiveHandlers = 0;
        var startedHandlers = 0;
        var completedHandlers = 0;
        var topology = new ReceiveEndpointTopology
        {
            QueueName = queueName,
            ExchangeName = exchangeName,
            Durable = false,
            AutoDelete = false,
            PrefetchCount = 2,
            ConcurrentMessageLimit = 2
        };
        var receiver = await transportFactory.CreateReceiveTransport(
            topology,
            async _ =>
            {
                lock (stateSync)
                {
                    activeHandlers++;
                    maximumActiveHandlers = Math.Max(maximumActiveHandlers, activeHandlers);
                    if (++startedHandlers == 2)
                        twoStarted.TrySetResult();
                }

                try
                {
                    await release.Task;
                }
                finally
                {
                    lock (stateSync)
                    {
                        activeHandlers--;
                        if (++completedHandlers == 5)
                            allCompleted.TrySetResult();
                    }
                }
            },
            messageType => messageType == expectedUrn);

        await receiver.Start();
        try
        {
            var serializer = new EnvelopeMessageSerializer();
            var sendTransport = await transportFactory.GetSendTransport(
                new Uri($"exchange:{exchangeName}?durable=false&autodelete=false"));
            for (var index = 0; index < 5; index++)
            {
                var context = new RabbitMqSendContext([typeof(CompatibilityMessage)], serializer)
                {
                    DestinationAddress = new Uri($"rabbitmq://localhost/exchange/{exchangeName}"),
                    MessageId = Guid.NewGuid().ToString()
                };
                await sendTransport.Send(new CompatibilityMessage { Value = index.ToString() }, context);
            }

            await twoStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(250);

            var probeFactory = new ConnectionFactory { Uri = new Uri(container.GetConnectionString()) };
            await using var probeConnection = await probeFactory.CreateConnectionAsync();
            await using var probeChannel = await probeConnection.CreateChannelAsync();
            var queueState = await probeChannel.QueueDeclarePassiveAsync(queueName);

            lock (stateSync)
            {
                Assert.Equal(2, maximumActiveHandlers);
                Assert.Equal(2, startedHandlers);
            }
            Assert.Equal(3u, queueState.MessageCount);

            release.TrySetResult();
            await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            release.TrySetResult();
            await receiver.Stop();
        }
    }

    private static RabbitMqTransportFactory CreateTransportFactory(RabbitMqContainer container)
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(container.GetConnectionString())
        };
        return new RabbitMqTransportFactory(
            new ConnectionProvider(connectionFactory),
            new RabbitMqFactoryConfigurator());
    }

    public sealed class CompatibilityMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class DeferredConfigurationConsumer : IConsumer<CompatibilityMessage>
    {
        public Task Consume(ConsumeContext<CompatibilityMessage> context) => Task.CompletedTask;
    }
}
