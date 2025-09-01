namespace MyServiceBus.Tests;

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
        configurator.UseExecute((ctx) =>
        {
            ctx.Calls.Add("A");
            return Task.CompletedTask;
        });
        configurator.UseExecute((ctx) =>
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
        var pipe = Pipe.Execute<TestContext>((ctx) =>
        {
            ctx.Calls.Add("X");
            return Task.CompletedTask;
        });

        var context = new TestContext();
        await pipe.Send(context);

        Assert.Equal(new[] { "X" }, context.Calls);
    }
}

