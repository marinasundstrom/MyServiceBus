using Azure.Messaging.ServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Shouldly;
using TestApp;

namespace MyServiceBus.AzureServiceBus.Tests;

[Collection("Azure Service Bus emulator")]
public sealed class CrossLanguageAzureServiceBusTests
{
    private const string ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    private const string BindingEntityName = "msb-compatibility-message";
    private static readonly Guid RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CorrelationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ConversationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid InitiatorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private const string NativeMessageId = "55555555-5555-5555-5555-555555555555";
    private const string ResponseAddress = "sb://localhost/msb-response";
    private const string FaultAddress = "sb://localhost/msb-publish_fault?type=topic";
    private const string SourceAddress = "sb://localhost/cross-language-source";
    private const string Subject = "cross-language-subject";
    private const string To = "cross-language-target";
    private const string Expiration = "60000";
    private const string HeaderValue = "cross-language-header-value";

    [AzureServiceBusEmulatorFact]
    public async Task Csharp_direct_send_delivers_to_java_consumer()
    {
        await SendFromCsharpToJava(
            "msb-direct",
            "azure-consume",
            new Uri("queue:msb-direct"),
            "direct-from-csharp");
    }

    [AzureServiceBusEmulatorFact]
    public async Task Csharp_publish_delivers_to_java_consumer()
    {
        await SendFromCsharpToJava(
            "msb-publish",
            "azure-consume",
            new Uri("topic:msb-compatibility-message"),
            "publish-from-csharp");
    }

    [AzureServiceBusEmulatorFact]
    public Task Java_direct_send_delivers_to_csharp_consumer() =>
        ReceiveFromJava("msb-direct", "azure-send", "msb-direct", "direct-from-java");

    [AzureServiceBusEmulatorFact]
    public Task Java_publish_delivers_to_csharp_consumer() =>
        ReceiveFromJava(
            "msb-publish",
            "azure-publish",
            "msb-compatibility-message",
            "publish-from-java");

    [AzureServiceBusEmulatorFact]
    public async Task Csharp_request_client_receives_java_response()
    {
        await PurgeQueue("msb-publish");
        await PurgeQueue("msb-response");
        const string expectedValue = "request-from-dotnet";
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            "azure-respond",
            "msb-publish",
            BindingEntityName,
            expectedValue);
        await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));

        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        configurator.SetTemporaryEndpointNameFormatter(_ => "msb-response");
        await using var client = new ServiceBusClient(ConnectionString);
        var factory = new AzureServiceBusTransportFactory(client, configurator);
        var requestClient = new GenericRequestClient<InteropRequest>(
            factory,
            new EnvelopeMessageSerializer(),
            new SendContextFactory(),
            factory.GetPublishAddress(BindingEntityName),
            RequestTimeout.After(TimeSpan.FromSeconds(20)));

        var response = await requestClient.GetResponseAsync<InteropResponse>(
            new InteropRequest { Value = expectedValue });
        await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RESPONDED", TimeSpan.FromSeconds(20));
        await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

        Assert.Equal(0, javaPeer.ExitCode);
        Assert.Equal("response-from-java", response.Message.Value);
    }

    [AzureServiceBusEmulatorFact]
    public async Task Java_request_client_receives_csharp_response()
    {
        await PurgeQueue("msb-publish");
        await PurgeQueue("msb-response");
        var server = MessageBus.Factory.Create<AzureServiceBusFactoryConfigurator>(cfg =>
        {
            cfg.Host(ConnectionString);
            cfg.UsePreProvisionedTopology();
            cfg.Message<InteropRequest>(message => message.SetEntityName(BindingEntityName));
            cfg.ReceiveEndpoint("msb-publish", endpoint =>
                endpoint.Handler<InteropRequest>(context =>
                    context.RespondAsync(new InteropResponse { Value = "response-from-dotnet" })));
        });

        await server.StartAsync(CancellationToken.None);
        try
        {
            using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
                ConnectionString,
                "azure-request",
                BindingEntityName,
                "unused",
                "request-from-java");
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromMinutes(2));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
            Assert.Equal(0, javaPeer.ExitCode);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    private static async Task SendFromCsharpToJava(
        string queueName,
        string javaMode,
        Uri destination,
        string expectedValue)
    {
        await PurgeQueue(queueName);
        using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
            ConnectionString,
            javaMode,
            queueName,
            BindingEntityName,
            expectedValue);
        await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "READY", TimeSpan.FromMinutes(2));

        await using var client = new ServiceBusClient(ConnectionString);
        var factory = CreateTransportFactory(client);
        var context = new SendContext([typeof(CrossLanguageMessage)], new EnvelopeMessageSerializer())
        {
            MessageId = NativeMessageId,
            RequestId = RequestId,
            CorrelationId = CorrelationId.ToString(),
            ConversationId = ConversationId,
            InitiatorId = InitiatorId,
            ResponseAddress = new Uri(ResponseAddress),
            FaultAddress = new Uri(FaultAddress),
            SourceAddress = new Uri(SourceAddress),
            DestinationAddress = destination
        };
        context.Headers["cross-language-header"] = HeaderValue;
        context.Headers["_subject"] = Subject;
        context.Headers["_to"] = To;
        context.Headers["_expiration"] = Expiration;
        var transport = await factory.GetSendTransport(destination);
        await transport.Send(new CrossLanguageMessage { Value = expectedValue }, context);

        await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
        await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
        Assert.Equal(0, javaPeer.ExitCode);
    }

    private static async Task ReceiveFromJava(
        string queueName,
        string javaMode,
        string destinationEntity,
        string expectedValue)
    {
        await PurgeQueue(queueName);
        await using var client = new ServiceBusClient(ConnectionString);
        var factory = CreateTransportFactory(client);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var topology = new ReceiveEndpointTransportTopology(
            queueName,
            durable: true,
            temporary: false,
            prefetchCount: 1,
            [new MessageBinding { MessageType = typeof(CrossLanguageMessage), EntityName = BindingEntityName }]);
        var receiveTransport = await factory.CreateReceiveTransport(
            topology,
            context =>
            {
                try
                {
                    context.RequestId.ShouldBe(RequestId);
                    context.CorrelationId.ShouldBe(CorrelationId);
                    context.ConversationId.ShouldBe(ConversationId);
                    context.InitiatorId.ShouldBe(InitiatorId);
                    context.ResponseAddress.ShouldBe(new Uri(ResponseAddress));
                    context.FaultAddress.ShouldBe(new Uri(FaultAddress));
                    context.Headers["message_id"].ShouldBe(NativeMessageId);
                    context.Headers["correlation_id"].ShouldBe(CorrelationId.ToString());
                    context.Headers["reply_to"].ShouldBe(ResponseAddress);
                    context.Headers["subject"].ShouldBe(Subject);
                    context.Headers["to"].ShouldBe(To);
                    context.Headers["expiration"].ShouldBe(Expiration);
                    context.Headers["cross-language-header"].ShouldBe(HeaderValue);
                    if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                        received.TrySetResult(message);
                }
                catch (Exception exception)
                {
                    received.TrySetException(exception);
                }
                return Task.CompletedTask;
            },
            urn => urn == MessageUrn.For(typeof(CrossLanguageMessage)));

        await receiveTransport.Start();
        try
        {
            using var javaPeer = AzureServiceBusJavaInteropPeer.Start(
                ConnectionString,
                javaMode,
                destinationEntity,
                "unused",
                expectedValue);
            await AzureServiceBusJavaInteropPeer.WaitForOutput(javaPeer, "SENT", TimeSpan.FromMinutes(2));
            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await AzureServiceBusJavaInteropPeer.WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

            Assert.Equal(0, javaPeer.ExitCode);
            Assert.Equal(expectedValue, message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    private static AzureServiceBusTransportFactory CreateTransportFactory(ServiceBusClient client)
    {
        var configurator = new AzureServiceBusFactoryConfigurator();
        configurator.Host(ConnectionString);
        configurator.UsePreProvisionedTopology();
        return new AzureServiceBusTransportFactory(client, configurator);
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
}
