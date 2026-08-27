using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using MassTransit;
using MyServiceBus.Topology;
using TestApp;

namespace MyServiceBus.AzureServiceBus.Tests;

[Collection("Azure Service Bus emulator")]
public sealed class MassTransitAzureServiceBusInteropTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CLOUD_CONNECTION_STRING")!;

    private static string DefaultTopicName =>
        new MassTransit.AzureServiceBusTransport.ServiceBusMessageNameFormatter()
            .GetMessageName(typeof(CrossLanguageMessage));

    private static string DefaultRequestTopicName =>
        new MassTransit.AzureServiceBusTransport.ServiceBusMessageNameFormatter()
            .GetMessageName(typeof(InteropRequest));

    [AzureServiceBusCloudFact]
    public async Task Csharp_MyServiceBus_request_client_receives_MassTransit_response()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-csharp-request-mt-{suffix}";
        var responseQueueName = $"msb-csharp-response-mt-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<InteropRequest>(context =>
                    context.RespondAsync(new InteropResponse { Value = "response-from-masstransit" })));
        });
        var requestConfigurator = new AzureServiceBusFactoryConfigurator();
        requestConfigurator.Host(ConnectionString);
        requestConfigurator.SetTemporaryEndpointNameFormatter(_ => responseQueueName);
        await using var serviceBusClient = new ServiceBusClient(ConnectionString);
        var requestFactory = new AzureServiceBusTransportFactory(serviceBusClient, requestConfigurator);
        var requestClient = new GenericRequestClient<InteropRequest>(
            requestFactory,
            new MyServiceBus.Serialization.EnvelopeMessageSerializer(),
            new SendContextFactory(),
            timeout: MyServiceBus.RequestTimeout.After(TimeSpan.FromSeconds(30)));

        await massTransit.StartAsync(CancellationToken.None);
        try
        {
            var response = await requestClient.GetResponseAsync<InteropResponse>(
                new InteropRequest { Value = "request-from-csharp" });

            Assert.Equal("response-from-masstransit", response.Message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
            await DeleteQueueIfExists(administrationClient, responseQueueName);
            await DeleteTopology(administrationClient, queueName, DefaultRequestTopicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task Java_MyServiceBus_request_client_receives_MassTransit_response()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-java-request-mt-{suffix}";
        var responseQueueName = $"msb-java-response-mt-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<InteropRequest>(context =>
                    context.RespondAsync(new InteropResponse { Value = "response-from-dotnet" })));
        });

        await massTransit.StartAsync(CancellationToken.None);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-request",
            DefaultRequestTopicName,
            responseQueueName,
            "request-from-java",
            createTopology: true);
        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromMinutes(2));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await DeleteQueueIfExists(administrationClient, responseQueueName);
            await DeleteTopology(administrationClient, queueName, DefaultRequestTopicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task MassTransit_request_client_receives_Csharp_MyServiceBus_response()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"mt-request-csharp-msb-{suffix}";
        var responseQueueName = $"mt-response-csharp-msb-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var myServiceBus = MyServiceBus.MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<InteropRequest>(context =>
                    context.RespondAsync(new InteropResponse { Value = "response-from-myservicebus" })));
        });
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.OverrideDefaultBusEndpointQueueName(responseQueueName);
        });

        await myServiceBus.StartAsync(CancellationToken.None);
        await massTransit.StartAsync(CancellationToken.None);
        try
        {
            var client = massTransit.CreateRequestClient<InteropRequest>(
                MassTransit.RequestTimeout.After(s: 30));
            var response = await client.GetResponse<InteropResponse>(
                new InteropRequest { Value = "request-from-masstransit" });

            Assert.Equal("response-from-myservicebus", response.Message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
            await myServiceBus.StopAsync(CancellationToken.None);
            await DeleteTopology(administrationClient, queueName, DefaultRequestTopicName);
            await DeleteQueueIfExists(administrationClient, responseQueueName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task MassTransit_request_client_receives_Java_MyServiceBus_response()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"mt-request-java-msb-{suffix}";
        var responseQueueName = $"mt-response-java-msb-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-respond",
            queueName,
            DefaultRequestTopicName,
            "request-from-masstransit",
            createTopology: true);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.OverrideDefaultBusEndpointQueueName(responseQueueName);
        });

        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));
            await massTransit.StartAsync(CancellationToken.None);
            var client = massTransit.CreateRequestClient<InteropRequest>(
                MassTransit.RequestTimeout.After(s: 30));
            var response = await client.GetResponse<InteropResponse>(
                new InteropRequest { Value = "request-from-masstransit" });

            Assert.Equal("response-from-java", response.Message.Value);
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RESPONDED", TimeSpan.FromSeconds(20));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await DeleteTopology(administrationClient, queueName, DefaultRequestTopicName);
            await DeleteQueueIfExists(administrationClient, responseQueueName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task Csharp_MyServiceBus_request_client_receives_MassTransit_fault()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-csharp-fault-mt-{suffix}";
        var responseQueueName = $"msb-csharp-fault-response-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint => endpoint.Consumer<MassTransitFaultingConsumer>());
        });
        var requestConfigurator = new AzureServiceBusFactoryConfigurator();
        requestConfigurator.Host(ConnectionString);
        requestConfigurator.SetTemporaryEndpointNameFormatter(_ => responseQueueName);
        await using var serviceBusClient = new ServiceBusClient(ConnectionString);
        var requestFactory = new AzureServiceBusTransportFactory(serviceBusClient, requestConfigurator);
        var requestClient = new GenericRequestClient<InteropRequest>(
            requestFactory,
            new MyServiceBus.Serialization.EnvelopeMessageSerializer(),
            new SendContextFactory(),
            timeout: MyServiceBus.RequestTimeout.After(TimeSpan.FromSeconds(30)));

        await massTransit.StartAsync(CancellationToken.None);
        try
        {
            var exception = await Assert.ThrowsAsync<MyServiceBus.RequestFaultException>(() =>
                requestClient.GetResponseAsync<InteropResponse>(
                    new InteropRequest { Value = "fault-from-masstransit" }));

            Assert.Contains("mass-transit-fault", exception.Fault.Exceptions[0].Message);
        }
        finally
        {
            await massTransit.StopAsync();
            await DeleteQueueIfExists(administrationClient, responseQueueName);
            await DeleteTopology(administrationClient, queueName, DefaultRequestTopicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task Java_MyServiceBus_request_client_receives_MassTransit_fault()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-java-fault-mt-{suffix}";
        var responseQueueName = $"msb-java-fault-response-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint => endpoint.Consumer<MassTransitFaultingConsumer>());
        });

        await massTransit.StartAsync(CancellationToken.None);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-request-fault",
            DefaultRequestTopicName,
            responseQueueName,
            "fault-from-java",
            createTopology: true);
        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "FAULT", TimeSpan.FromMinutes(2));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await DeleteQueueIfExists(administrationClient, responseQueueName);
            await DeleteTopology(administrationClient, queueName, DefaultRequestTopicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task MassTransit_request_client_receives_Csharp_MyServiceBus_fault()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"mt-fault-csharp-msb-{suffix}";
        var responseQueueName = $"mt-fault-response-csharp-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var myServiceBus = MyServiceBus.MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<InteropRequest>(_ =>
                    Task.FromException(new InvalidOperationException("myservicebus-fault"))));
        });
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.OverrideDefaultBusEndpointQueueName(responseQueueName);
        });

        await myServiceBus.StartAsync(CancellationToken.None);
        await massTransit.StartAsync(CancellationToken.None);
        try
        {
            var client = massTransit.CreateRequestClient<InteropRequest>(
                MassTransit.RequestTimeout.After(s: 30));
            var exception = await Assert.ThrowsAsync<MassTransit.RequestFaultException>(() =>
                client.GetResponse<InteropResponse>(
                    new InteropRequest { Value = "fault-from-myservicebus" }));

            Assert.Contains("myservicebus-fault", exception.Message);
        }
        finally
        {
            await massTransit.StopAsync();
            await myServiceBus.StopAsync(CancellationToken.None);
            await DeleteTopology(administrationClient, queueName, DefaultRequestTopicName);
            await DeleteQueueIfExists(administrationClient, responseQueueName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task MassTransit_request_client_receives_Java_MyServiceBus_fault()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"mt-fault-java-msb-{suffix}";
        var responseQueueName = $"mt-fault-response-java-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-fault",
            queueName,
            DefaultRequestTopicName,
            "fault-from-masstransit",
            createTopology: true);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.OverrideDefaultBusEndpointQueueName(responseQueueName);
        });

        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));
            await massTransit.StartAsync(CancellationToken.None);
            var client = massTransit.CreateRequestClient<InteropRequest>(
                MassTransit.RequestTimeout.After(s: 30));
            var responseTask = client.GetResponse<InteropResponse>(
                new InteropRequest { Value = "fault-from-masstransit" });
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "FAULTING", TimeSpan.FromSeconds(20));
            var exception = await Assert.ThrowsAsync<MassTransit.RequestFaultException>(() => responseTask);

            Assert.Contains("java-fault", exception.Message);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await DeleteTopology(administrationClient, queueName, DefaultRequestTopicName);
            await DeleteQueueIfExists(administrationClient, responseQueueName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task MassTransit_publish_is_consumed_by_default_named_Csharp_MyServiceBus_endpoint()
    {
        var queueName = $"msb-mt-default-csharp-{Guid.NewGuid():N}"[..34];
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var myServiceBus = MyServiceBus.MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<CrossLanguageMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                }));
        });
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg => cfg.Host(ConnectionString));

        await myServiceBus.StartAsync(CancellationToken.None);
        await massTransit.StartAsync(CancellationToken.None);
        try
        {
            await massTransit.Publish(new CrossLanguageMessage { Value = "masstransit-default-to-csharp" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("masstransit-default-to-csharp", message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
            await myServiceBus.StopAsync(CancellationToken.None);
            await DeleteTopology(administrationClient, queueName, DefaultTopicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task MassTransit_publish_is_consumed_by_default_named_Java_MyServiceBus_endpoint()
    {
        var queueName = $"msb-mt-default-java-{Guid.NewGuid():N}"[..32];
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-consume-default",
            queueName,
            "unused",
            "masstransit-default-to-java",
            createTopology: true);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg => cfg.Host(ConnectionString));

        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));
            await massTransit.StartAsync(CancellationToken.None);
            await massTransit.Publish(new CrossLanguageMessage { Value = "masstransit-default-to-java" });

            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await DeleteTopology(administrationClient, queueName, DefaultTopicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task Default_named_Csharp_MyServiceBus_publish_is_consumed_by_MassTransit()
    {
        var queueName = $"msb-csharp-default-mt-{Guid.NewGuid():N}"[..34];
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<CrossLanguageMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                }));
        });
        var myServiceBus = MyServiceBus.MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
            cfg.Host(ConnectionString));

        await massTransit.StartAsync(CancellationToken.None);
        await myServiceBus.StartAsync(CancellationToken.None);
        try
        {
            await myServiceBus.Publish(new CrossLanguageMessage { Value = "csharp-default-to-masstransit" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("csharp-default-to-masstransit", message.Value);
        }
        finally
        {
            await myServiceBus.StopAsync(CancellationToken.None);
            await massTransit.StopAsync();
            await DeleteTopology(administrationClient, queueName, DefaultTopicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task Default_named_Java_MyServiceBus_publish_is_consumed_by_MassTransit()
    {
        var queueName = $"msb-java-default-mt-{Guid.NewGuid():N}"[..32];
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<CrossLanguageMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                }));
        });
        await massTransit.StartAsync(CancellationToken.None);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-publish-default",
            "unused",
            "unused",
            "java-default-to-masstransit");
        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromMinutes(2));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("java-default-to-masstransit", message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await DeleteTopology(administrationClient, queueName, DefaultTopicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task Csharp_MyServiceBus_direct_send_is_consumed_by_MassTransit()
    {
        var queueName = $"msb-csharp-send-mt-{Guid.NewGuid():N}"[..32];
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<CrossLanguageMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                }));
        });
        var myServiceBus = MyServiceBus.MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
            cfg.Host(ConnectionString));

        await massTransit.StartAsync(CancellationToken.None);
        await myServiceBus.StartAsync(CancellationToken.None);
        try
        {
            var endpoint = await myServiceBus.GetSendEndpoint(new Uri($"queue:{queueName}"));
            await endpoint.Send(new CrossLanguageMessage { Value = "csharp-send-to-masstransit" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("csharp-send-to-masstransit", message.Value);
        }
        finally
        {
            await myServiceBus.StopAsync(CancellationToken.None);
            await massTransit.StopAsync();
            await DeleteTopology(administrationClient, queueName, DefaultTopicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task Java_MyServiceBus_direct_send_is_consumed_by_MassTransit()
    {
        var queueName = $"msb-java-send-mt-{Guid.NewGuid():N}"[..30];
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var massTransit = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.ReceiveEndpoint(queueName, endpoint =>
                endpoint.Handler<CrossLanguageMessage>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                }));
        });

        await massTransit.StartAsync(CancellationToken.None);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-send-public",
            queueName,
            "unused",
            "java-send-to-masstransit");
        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromMinutes(2));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("java-send-to-masstransit", message.Value);
        }
        finally
        {
            await massTransit.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await DeleteTopology(administrationClient, queueName, DefaultTopicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task MassTransit_send_is_consumed_by_Csharp_MyServiceBus()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-mt-csharp-{suffix}";
        var topicName = $"msb-mt-message-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        await using var client = new ServiceBusClient(ConnectionString);
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        var factory = new AzureServiceBusTransportFactory(client, configurator);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var topology = new ReceiveEndpointTransportTopology(
            queueName,
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CrossLanguageMessage), EntityName = topicName }]);
        var receiveTransport = await factory.CreateReceiveTransport(
            topology,
            context =>
            {
                if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                    received.TrySetResult(message);
                return Task.CompletedTask;
            },
            urn => urn == MessageUrn.For(typeof(CrossLanguageMessage)));
        var bus = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg => cfg.Host(ConnectionString));

        await receiveTransport.Start();
        using var startTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await bus.StartAsync(startTimeout.Token);
        try
        {
            var endpoint = await bus.GetSendEndpoint(new Uri($"queue:{queueName}"));
            await endpoint.Send(new CrossLanguageMessage { Value = "masstransit-to-csharp" });

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("masstransit-to-csharp", message.Value);
        }
        finally
        {
            await bus.StopAsync();
            await receiveTransport.Stop();
            await DeleteTopology(administrationClient, queueName, topicName);
        }
    }

    [AzureServiceBusCloudFact]
    public async Task MassTransit_send_is_consumed_by_Java_MyServiceBus()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var queueName = $"msb-mt-java-{suffix}";
        var topicName = $"msb-mt-message-{suffix}";
        var administrationClient = new ServiceBusAdministrationClient(ConnectionString);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-consume-value",
            queueName,
            topicName,
            "masstransit-to-java",
            createTopology: true);
        var bus = MassTransit.Bus.Factory.CreateUsingAzureServiceBus(cfg => cfg.Host(ConnectionString));

        try
        {
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));
            using var startTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await bus.StartAsync(startTimeout.Token);
            var endpoint = await bus.GetSendEndpoint(new Uri($"queue:{queueName}"));
            await endpoint.Send(new CrossLanguageMessage { Value = "masstransit-to-java" });

            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await bus.StopAsync();
            if (!javaPeer.HasExited)
                javaPeer.Kill(entireProcessTree: true);
            await DeleteTopology(administrationClient, queueName, topicName);
        }
    }

    private static async Task DeleteTopology(
        ServiceBusAdministrationClient administrationClient,
        string queueName,
        string topicName)
    {
        await DeleteQueueIfExists(administrationClient, queueName);
        await DeleteQueueIfExists(administrationClient, queueName + "_error");
        await DeleteQueueIfExists(administrationClient, queueName + "_skipped");
        await DeleteTopicIfExists(administrationClient, topicName);
        await DeleteTopicIfExists(administrationClient, queueName + "_fault");
    }

    private static async Task DeleteQueueIfExists(
        ServiceBusAdministrationClient administrationClient,
        string queueName)
    {
        if ((await administrationClient.QueueExistsAsync(queueName)).Value)
            await administrationClient.DeleteQueueAsync(queueName);
    }

    private static async Task DeleteTopicIfExists(
        ServiceBusAdministrationClient administrationClient,
        string topicName)
    {
        if ((await administrationClient.TopicExistsAsync(topicName)).Value)
            await administrationClient.DeleteTopicAsync(topicName);
    }

    private sealed class MassTransitFaultingConsumer : MassTransit.IConsumer<InteropRequest>
    {
        public Task Consume(MassTransit.ConsumeContext<InteropRequest> context)
        {
            throw new InvalidOperationException("mass-transit-fault");
        }
    }
}
