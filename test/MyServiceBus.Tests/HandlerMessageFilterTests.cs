using System;
using System.IO;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;

namespace MyServiceBus.Tests;

public class HandlerMessageFilterTests
{
    [Fact]
    [Throws(typeof(FileNotFoundException), typeof(FileLoadException), typeof(BadImageFormatException))]
    public async Task Invokes_handler_and_next()
    {
        bool handlerCalled = false;
        bool nextCalled = false;

        Func<ConsumeContext<string>, Task> handler = ctx =>
        {
            handlerCalled = true;
            return Task.CompletedTask;
        };

        var filterType = typeof(MessageBus).Assembly.GetType("MyServiceBus.HandlerMessageFilter`1")!;
        var genericType = filterType.MakeGenericType(typeof(string));
        var filter = (IFilter<ConsumeContext<string>>)Activator.CreateInstance(genericType, handler)!;

        var next = Substitute.For<IPipe<ConsumeContext<string>>>();
        next.Send(Arg.Any<ConsumeContext<string>>()).Returns(Task.CompletedTask).AndDoes(_ => nextCalled = true);

        await filter.Send(new DefaultConsumeContext<string>("hi"), next);

        Assert.True(handlerCalled);
        Assert.True(nextCalled);
    }
}
