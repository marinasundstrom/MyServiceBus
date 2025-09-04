namespace MyServiceBus.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus;
using Xunit;

public class PipeTests
{
    class TestContext : BasePipeContext
    {
        public TestContext() : base(CancellationToken.None)
        {
        }

        public IList<string> Calls { get; } = new List<string>();
    }

    [Fact]
    public async Task Executes_Filters_In_Order()
    {
        var configurator = new PipeConfigurator<TestContext>();
        configurator.UseExecute([Throws(typeof(NotSupportedException))] (ctx) =>
        {
            ctx.Calls.Add("A");
            return Task.CompletedTask;
        });
        configurator.UseExecute([Throws(typeof(NotSupportedException))] (ctx) =>
        {
            ctx.Calls.Add("B");
            return Task.CompletedTask;
        });

        var pipe = configurator.Build();
        var context = new TestContext();
        await pipe.Send(context);

        Assert.Equal(new[] { "A", "B" }, context.Calls);
    }

    [Fact]
    public async Task Execute_Pipe_Invokes_Callback()
    {
        var pipe = Pipe.Execute<TestContext>([Throws(typeof(NotSupportedException))] (ctx) =>
        {
            ctx.Calls.Add("X");
            return Task.CompletedTask;
        });

        var context = new TestContext();
        await pipe.Send(context);

        Assert.Equal(new[] { "X" }, context.Calls);
    }

    [Fact]
    public async Task Retry_Filter_Retries_On_Failure()
    {
        var configurator = new PipeConfigurator<TestContext>();
        var attempts = 0;
        configurator.UseRetry(2);
        configurator.UseExecute([Throws(typeof(InvalidOperationException))] (ctx) =>
        {
            attempts++;
            if (attempts < 3)
                throw new InvalidOperationException("fail");
            ctx.Calls.Add("done");
            return Task.CompletedTask;
        });

        var pipe = configurator.Build();
        var context = new TestContext();

        await pipe.Send(context);

        Assert.Equal(3, attempts);
        Assert.Equal(new[] { "done" }, context.Calls);
    }

    [Fact]
    public async Task MessageRetry_Retries_On_Failure()
    {
        var configurator = new PipeConfigurator<TestContext>();
        var attempts = 0;
        configurator.UseMessageRetry(r => r.Immediate(2));
        configurator.UseExecute([Throws(typeof(InvalidOperationException))] (ctx) =>
        {
            attempts++;
            if (attempts < 3)
                throw new InvalidOperationException("fail");
            ctx.Calls.Add("done");
            return Task.CompletedTask;
        });

        var pipe = configurator.Build();
        var context = new TestContext();

        await pipe.Send(context);

        Assert.Equal(3, attempts);
        Assert.Equal(new[] { "done" }, context.Calls);
    }
}

