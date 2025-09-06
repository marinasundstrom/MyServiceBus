using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;

namespace MyServiceBus.Tests;

public class DefaultConsumeContextTests
{
    class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    [Fact]
    [Throws(typeof(InvalidOperationException))]
    public async Task GetSendEndpoint_throws_when_provider_missing()
    {
        var context = new DefaultConsumeContext<TestMessage>(new TestMessage());
        await Assert.ThrowsAsync<InvalidOperationException>([Throws(typeof(UriFormatException))] () => context.GetSendEndpoint(new Uri("queue:test")));
    }

    [Fact]
    [Throws(typeof(UriFormatException))]
    public async Task Forward_uses_send_endpoint_provider()
    {
        var sendEndpoint = Substitute.For<ISendEndpoint>();
        var provider = Substitute.For<ISendEndpointProvider>();
        provider.GetSendEndpoint(new Uri("queue:test")).Returns(Task.FromResult(sendEndpoint));

        var context = new DefaultConsumeContext<TestMessage>(new TestMessage(), provider);
        await context.Forward<TestMessage>(new Uri("queue:test"), new TestMessage(), CancellationToken.None);

        await sendEndpoint.Received().Send<TestMessage>(Arg.Any<object>(), Arg.Any<Action<ISendContext>>(), Arg.Any<CancellationToken>());
    }
}
