using System.Reflection;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using MyServiceBus.Serialization;

namespace MyServiceBus.AmazonSqs.Tests;

public class ReceiveContractTests
{
    [Theory]
    [InlineData("urn:message:Contracts:Base", 1, 0)]
    [InlineData("urn:message:Contracts:Unknown", 0, 1)]
    public async Task Matches_all_advertised_contracts_before_settlement(string accepted, int deliveries, int skips)
    {
        using var sqs = new TestSqs();
        var handled = 0;
        Func<ReceiveContext, Task> handler = _ => { handled++; return Task.CompletedTask; };
        Func<string?, bool> matches = urn => urn == accepted;
        var transport = (AmazonSqsReceiveTransport)Activator.CreateInstance(typeof(AmazonSqsReceiveTransport),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [sqs, "input", "skipped", "input", false, 1, 30, 1, 1, handler, matches, null, null, new InboundMessageResolver(), null], null)!;
        var message = new Message { Attributes = new(), MessageAttributes = new(), ReceiptHandle = "receipt", Body = """{"messageId":"b4c5ac9a-29fe-4ae1-91ea-f8d13f5025e4","messageType":["urn:message:Contracts:Concrete","urn:message:Contracts:Base"],"message":{"value":1}}""" };
        using var semaphore = new SemaphoreSlim(1);
        await (Task)typeof(AmazonSqsReceiveTransport).GetMethod("ProcessMessage", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(transport, [message, semaphore, CancellationToken.None])!;
        Assert.Equal(deliveries, handled);
        Assert.Equal(skips, sqs.Sent);
        Assert.Equal(1, sqs.Deleted);
    }

    private sealed class TestSqs() : AmazonSQSClient(new AnonymousAWSCredentials(), new AmazonSQSConfig { ServiceURL = "http://localhost:4566" })
    {
        public int Sent;
        public int Deleted;
        public override Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
        { Sent++; return Task.FromResult(new SendMessageResponse()); }
        public override Task<DeleteMessageResponse> DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = default)
        { Deleted++; return Task.FromResult(new DeleteMessageResponse()); }
    }
}
