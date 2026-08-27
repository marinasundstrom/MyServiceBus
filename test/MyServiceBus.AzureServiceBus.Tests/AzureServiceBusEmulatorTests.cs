using Azure.Messaging.ServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MyServiceBus.AzureServiceBus.Tests;

[CollectionDefinition("Azure Service Bus emulator", DisableParallelization = true)]
public sealed class AzureServiceBusEmulatorCollection;

[Collection("Azure Service Bus emulator")]
public sealed class AzureServiceBusEmulatorTests
{
    private const string ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    [AzureServiceBusEmulatorFact]
    public async Task Queue_transport_round_trips_a_MassTransit_envelope()
    {
        await PurgeQueue("msb-direct");
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        await using var client = new ServiceBusClient(ConnectionString);
        var factory = new AzureServiceBusTransportFactory(client, configurator);
        var received = new TaskCompletionSource<CompatibilityMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedUrn = MessageUrn.For(typeof(CompatibilityMessage));
        var topology = new ReceiveEndpointTransportTopology(
            "msb-direct",
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CompatibilityMessage), EntityName = "msb-compatibility-message" }]);
        var receiveTransport = await factory.CreateReceiveTransport(
            topology,
            context =>
            {
                if (context.TryGetMessage<CompatibilityMessage>(out var message))
                    received.TrySetResult(message);
                return Task.CompletedTask;
            },
            urn => urn == expectedUrn);

        await receiveTransport.Start();
        try
        {
            var sendContext = new SendContext(
                [typeof(CompatibilityMessage)],
                new EnvelopeMessageSerializer())
            {
                MessageId = Guid.NewGuid().ToString(),
                DestinationAddress = new Uri("sb://localhost/msb-direct")
            };
            var sendTransport = await factory.GetSendTransport(new Uri("queue:msb-direct"));
            await sendTransport.Send(new CompatibilityMessage { Value = "from-dotnet" }, sendContext);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("from-dotnet", message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Competing_consumers_deliver_each_message_once()
    {
        const int messageCount = 20;
        await PurgeQueue("msb-direct");
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        await using var client = new ServiceBusClient(ConnectionString);
        var factory = new AzureServiceBusTransportFactory(client, configurator);
        var deliveries = new ConcurrentDictionary<string, int>();
        var consumers = new ConcurrentDictionary<string, byte>();
        var delivered = 0;
        var allDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var topology = new ReceiveEndpointTransportTopology(
            "msb-direct",
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CompatibilityMessage), EntityName = "msb-compatibility-message" }]);

        Func<string, Func<ReceiveContext, Task>> handler = consumer => async context =>
        {
            if (context.TryGetMessage<CompatibilityMessage>(out var message))
            {
                consumers.TryAdd(consumer, 0);
                deliveries.AddOrUpdate(message.Value, 1, (_, count) => count + 1);
                if (Interlocked.Increment(ref delivered) == messageCount)
                    allDelivered.TrySetResult();
                await Task.Delay(25);
            }
        };
        var first = await factory.CreateReceiveTransport(topology, handler("first"));
        var second = await factory.CreateReceiveTransport(topology, handler("second"));

        await first.Start();
        await second.Start();
        try
        {
            var sendTransport = await factory.GetSendTransport(new Uri("queue:msb-direct"));
            for (var index = 0; index < messageCount; index++)
            {
                var context = new SendContext([typeof(CompatibilityMessage)], new EnvelopeMessageSerializer())
                {
                    MessageId = Guid.NewGuid().ToString(),
                    DestinationAddress = new Uri("sb://localhost/msb-direct")
                };
                await sendTransport.Send(new CompatibilityMessage { Value = $"competing-{index}" }, context);
            }

            await allDelivered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await Task.Delay(250);
            Assert.Equal(messageCount, deliveries.Count);
            Assert.All(deliveries.Values, count => Assert.Equal(1, count));
            Assert.Equal(2, consumers.Count);
        }
        finally
        {
            await second.Stop();
            await first.Stop();
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Topic_publish_is_forwarded_to_the_endpoint_queue()
    {
        await PurgeQueue("msb-publish");
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        await using var client = new ServiceBusClient(ConnectionString);
        var factory = new AzureServiceBusTransportFactory(client, configurator);
        var received = new TaskCompletionSource<CompatibilityMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedUrn = MessageUrn.For(typeof(CompatibilityMessage));
        var topology = new ReceiveEndpointTransportTopology(
            "msb-publish",
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CompatibilityMessage), EntityName = "msb-compatibility-message" }]);
        var receiveTransport = await factory.CreateReceiveTransport(
            topology,
            context =>
            {
                if (context.TryGetMessage<CompatibilityMessage>(out var message))
                    received.TrySetResult(message);
                return Task.CompletedTask;
            },
            urn => urn == expectedUrn);

        await receiveTransport.Start();
        try
        {
            var sendContext = new SendContext(
                [typeof(CompatibilityMessage)],
                new EnvelopeMessageSerializer())
            {
                MessageId = Guid.NewGuid().ToString(),
                DestinationAddress = factory.GetPublishAddress("msb-compatibility-message")
            };
            var sendTransport = await factory.GetSendTransport(sendContext.DestinationAddress);
            await sendTransport.Send(new CompatibilityMessage { Value = "published-from-dotnet" }, sendContext);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("published-from-dotnet", message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task MassTransit_familiar_factory_configuration_publishes_to_a_handler()
    {
        await PurgeQueue("msb-publish");
        var received = new TaskCompletionSource<CompatibilityMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bus = MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.UsePreProvisionedTopology();
            cfg.Message<CompatibilityMessage>(message =>
                message.SetEntityName("msb-compatibility-message"));
            cfg.ReceiveEndpoint("msb-publish", endpoint =>
                endpoint.Handler<CompatibilityMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                }));
        });

        await bus.StartAsync(CancellationToken.None);
        try
        {
            await bus.Publish(new CompatibilityMessage { Value = "configured-dotnet-bus" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("configured-dotnet-bus", message.Value);
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Retry_exhaustion_moves_the_message_to_error_and_publishes_a_fault()
    {
        await PurgeQueue("msb-publish");
        await PurgeQueue("msb-publish_error");
        await PurgeQueue("msb-publish-fault-observer");
        var attempts = 0;
        var bus = MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.UsePreProvisionedTopology();
            cfg.Message<CompatibilityMessage>(message =>
                message.SetEntityName("msb-compatibility-message"));
            cfg.ReceiveEndpoint("msb-publish", endpoint =>
            {
                endpoint.UseMessageRetry(retry => retry.Immediate(2));
                endpoint.Handler<CompatibilityMessage>(_ =>
                {
                    Interlocked.Increment(ref attempts);
                    return Task.FromException(new InvalidOperationException("emulator-retry-exhausted"));
                });
            });
        });

        await bus.StartAsync(CancellationToken.None);
        try
        {
            await bus.Publish(new CompatibilityMessage { Value = "failed-dotnet-message" });
            var errorMessage = await ReceiveOne("msb-publish_error");
            var faultMessage = await ReceiveOne("msb-publish-fault-observer");
            var errorEnvelope = JsonSerializer.Deserialize<Envelope<CompatibilityMessage>>(
                errorMessage.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.Equal(3, Volatile.Read(ref attempts));
            Assert.Equal("failed-dotnet-message", errorEnvelope?.Message.Value);
            Assert.Equal(
                "emulator-retry-exhausted",
                errorEnvelope?.Headers[MessageHeaders.ExceptionMessage].ToString());
            Assert.Contains("Fault", faultMessage.Body.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Retry_recovers_without_using_failure_destinations()
    {
        await PurgeQueue("msb-publish");
        await PurgeQueue("msb-publish_error");
        await PurgeQueue("msb-publish-fault-observer");
        var attempts = 0;
        var consumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bus = MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.UsePreProvisionedTopology();
            cfg.Message<CompatibilityMessage>(message =>
                message.SetEntityName("msb-compatibility-message"));
            cfg.ReceiveEndpoint("msb-publish", endpoint =>
            {
                endpoint.UseMessageRetry(retry => retry.Immediate(2));
                endpoint.Handler<CompatibilityMessage>(_ =>
                {
                    if (Interlocked.Increment(ref attempts) < 3)
                        return Task.FromException(new InvalidOperationException("emulator-retry"));

                    consumed.TrySetResult();
                    return Task.CompletedTask;
                });
            });
        });

        await bus.StartAsync(CancellationToken.None);
        try
        {
            await bus.Publish(new CompatibilityMessage { Value = "eventually-consumed" });
            await consumed.Task.WaitAsync(TimeSpan.FromSeconds(20));

            Assert.Equal(3, Volatile.Read(ref attempts));
            Assert.Null(await TryReceiveOne("msb-publish_error", TimeSpan.FromMilliseconds(500)));
            Assert.Null(await TryReceiveOne("msb-publish-fault-observer", TimeSpan.FromMilliseconds(500)));
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Unregistered_message_is_preserved_in_the_skipped_queue()
    {
        await PurgeQueue("msb-publish");
        await PurgeQueue("msb-publish_skipped");
        await using var client = new ServiceBusClient(ConnectionString);
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        var factory = new AzureServiceBusTransportFactory(client, configurator);
        var topology = new ReceiveEndpointTransportTopology(
            "msb-publish",
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CompatibilityMessage), EntityName = "msb-compatibility-message" }]);
        var receiveTransport = await factory.CreateReceiveTransport(
            topology,
            _ => Task.FromException(new InvalidOperationException("An unregistered message reached the handler.")),
            urn => urn == MessageUrn.For(typeof(CompatibilityMessage)));

        await receiveTransport.Start();
        try
        {
            var context = new SendContext([typeof(UnregisteredMessage)], new EnvelopeMessageSerializer())
            {
                MessageId = Guid.NewGuid().ToString(),
                DestinationAddress = new Uri("sb://localhost/msb-publish")
            };
            var sendTransport = await factory.GetSendTransport(new Uri("queue:msb-publish"));
            await sendTransport.Send(new UnregisteredMessage { Value = "skipped-dotnet-message" }, context);

            var skippedMessage = await ReceiveOne("msb-publish_skipped");
            var skippedEnvelope = JsonSerializer.Deserialize<Envelope<UnregisteredMessage>>(
                skippedMessage.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal("skipped-dotnet-message", skippedEnvelope?.Message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Request_client_receives_a_correlated_response()
    {
        await PurgeQueue("msb-publish");
        await PurgeQueue("msb-response");
        var server = MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.UsePreProvisionedTopology();
            cfg.Message<RequestMessage>(message =>
                message.SetEntityName("msb-compatibility-message"));
            cfg.ReceiveEndpoint("msb-publish", endpoint =>
                endpoint.Handler<RequestMessage>(context =>
                    context.RespondAsync(new ResponseMessage
                    {
                        Value = "response-to-" + context.Message.Value
                    })));
        });

        await server.StartAsync(CancellationToken.None);
        try
        {
            var configurator = new AzureServiceBusFactoryConfigurator();
            configurator.Host(ConnectionString);
            configurator.UsePreProvisionedTopology();
            configurator.SetTemporaryEndpointNameFormatter(_ => "msb-response");
            await using var client = new ServiceBusClient(ConnectionString);
            var factory = new AzureServiceBusTransportFactory(client, configurator);
            var requestClient = new GenericRequestClient<RequestMessage>(
                factory,
                new EnvelopeMessageSerializer(),
                new SendContextFactory(),
                factory.GetPublishAddress("msb-compatibility-message"),
                RequestTimeout.After(TimeSpan.FromSeconds(20)));

            var response = await requestClient.GetResponseAsync<ResponseMessage>(
                new RequestMessage { Value = "dotnet-request" });

            Assert.Equal("response-to-dotnet-request", response.Message.Value);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [AzureServiceBusEmulatorFact]
    public async Task Request_client_receives_a_correlated_fault()
    {
        await PurgeQueue("msb-publish");
        await PurgeQueue("msb-publish_error");
        await PurgeQueue("msb-response");
        var server = MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.UsePreProvisionedTopology();
            cfg.Message<RequestMessage>(message =>
                message.SetEntityName("msb-compatibility-message"));
            cfg.ReceiveEndpoint("msb-publish", endpoint =>
                endpoint.Handler<RequestMessage>(_ =>
                    Task.FromException(new InvalidOperationException("request-handler-fault"))));
        });

        await server.StartAsync(CancellationToken.None);
        try
        {
            var configurator = new AzureServiceBusFactoryConfigurator();
            configurator.Host(ConnectionString);
            configurator.UsePreProvisionedTopology();
            configurator.SetTemporaryEndpointNameFormatter(_ => "msb-response");
            await using var client = new ServiceBusClient(ConnectionString);
            var factory = new AzureServiceBusTransportFactory(client, configurator);
            var requestClient = new GenericRequestClient<RequestMessage>(
                factory,
                new EnvelopeMessageSerializer(),
                new SendContextFactory(),
                factory.GetPublishAddress("msb-compatibility-message"),
                RequestTimeout.After(TimeSpan.FromSeconds(20)));

            var exception = await Assert.ThrowsAsync<RequestFaultException>(() =>
                requestClient.GetResponseAsync<ResponseMessage>(
                    new RequestMessage { Value = "faulting-dotnet-request" }));

            Assert.Contains("RequestMessage", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    private static async Task PurgeQueue(string queueName)
    {
        await using var client = new ServiceBusClient(ConnectionString);
        await using var receiver = client.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        while ((await receiver.ReceiveMessagesAsync(100, TimeSpan.FromMilliseconds(250))).Count > 0)
        {
        }
    }

    private static async Task<ServiceBusReceivedMessage> ReceiveOne(string queueName)
    {
        await using var client = new ServiceBusClient(ConnectionString);
        await using var receiver = client.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        return await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(20))
            ?? throw new TimeoutException($"No message arrived on '{queueName}'.");
    }

    private static async Task<ServiceBusReceivedMessage?> TryReceiveOne(string queueName, TimeSpan timeout)
    {
        await using var client = new ServiceBusClient(ConnectionString);
        await using var receiver = client.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        return await receiver.ReceiveMessageAsync(timeout);
    }

    [EntityName("msb-compatibility-message")]
    public sealed class CompatibilityMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class UnregisteredMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class RequestMessage
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class ResponseMessage
    {
        public string Value { get; set; } = string.Empty;
    }
}
