using System.Reflection;
using Azure.Messaging.ServiceBus;
using MyServiceBus.Serialization;

namespace MyServiceBus.AzureServiceBus.Tests;

public class ReceiveContractTests
{
    [Theory]
    [InlineData("urn:message:Contracts:Base", 1, 0)]
    [InlineData("urn:message:Contracts:Unknown", 0, 1)]
    public async Task Matches_all_advertised_contracts_before_settlement(string accepted, int deliveries, int skips)
    {
        var processor = new TestProcessor();
        var sender = new TestSender();
        var receiver = new TestReceiver();
        var handled = 0;
        Func<ReceiveContext, Task> handler = _ => { handled++; return Task.CompletedTask; };
        Func<string?, bool> matches = urn => urn == accepted;
        _ = (AzureServiceBusReceiveTransport)Activator.CreateInstance(typeof(AzureServiceBusReceiveTransport),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [processor, sender, "input", handler, matches, null, null, new InboundMessageResolver(), null], null)!;
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("""{"messageId":"b4c5ac9a-29fe-4ae1-91ea-f8d13f5025e4","messageType":["urn:message:Contracts:Concrete","urn:message:Contracts:Base"],"message":{"value":1}}"""),
            contentType: InboundMessageResolver.EnvelopeContentType, timeToLive: TimeSpan.FromHours(1), messageId: "message-1");
        await processor.Deliver(new ProcessMessageEventArgs(message, receiver, default));
        Assert.Equal(deliveries, handled);
        Assert.Equal(skips, sender.Sent);
        Assert.Equal(1, receiver.Completed);
    }

    private sealed class TestProcessor : ServiceBusProcessor
    {
        public Task Deliver(ProcessMessageEventArgs args) => OnProcessMessageAsync(args);
    }
    private sealed class TestSender : ServiceBusSender
    {
        public int Sent;
        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        { Sent++; return Task.CompletedTask; }
    }
    private sealed class TestReceiver : ServiceBusReceiver
    {
        public int Completed;
        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        { Completed++; return Task.CompletedTask; }
    }
}
