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
        public TestContext(CancellationToken cancellationToken = default) : base(cancellationToken)
        {
        }

        public IList<string> Calls { get; } = new List<string>();
    }

    sealed class RecordingFilter(string name) : IFilter<TestContext>
    {
        public async Task Send(TestContext context, IPipe<TestContext> next)
        {
            context.Calls.Add($"{name}:before");
            await next.Send(context);
            context.Calls.Add($"{name}:after");
        }
    }

    sealed class ShortCircuitFilter : IFilter<TestContext>
    {
        public Task Send(TestContext context, IPipe<TestContext> next)
        {
            context.Calls.Add("stopped");
            return Task.CompletedTask;
        }
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
    public async Task Filters_wrap_downstream_in_registration_order()
    {
        var configurator = new PipeConfigurator<TestContext>();
        configurator.UseFilter(new RecordingFilter("outer"));
        configurator.UseFilter(new RecordingFilter("inner"));

        var context = new TestContext();
        await configurator.Build().Send(context);

        Assert.Equal(
            new[] { "outer:before", "inner:before", "inner:after", "outer:after" },
            context.Calls);
    }

    [Fact]
    public async Task Filter_can_short_circuit_downstream_pipeline()
    {
        var configurator = new PipeConfigurator<TestContext>();
        configurator.UseFilter(new ShortCircuitFilter());
        configurator.UseExecute(context =>
        {
            context.Calls.Add("downstream");
            return Task.CompletedTask;
        });

        var context = new TestContext();
        await configurator.Build().Send(context);

        Assert.Equal(new[] { "stopped" }, context.Calls);
    }

    [Fact]
    public async Task Exception_stops_pipeline_and_propagates_unchanged()
    {
        var expected = new InvalidOperationException("failed");
        var configurator = new PipeConfigurator<TestContext>();
        configurator.UseExecute(_ => Task.FromException(expected));
        configurator.UseExecute(context =>
        {
            context.Calls.Add("downstream");
            return Task.CompletedTask;
        });

        var context = new TestContext();
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => configurator.Build().Send(context));

        Assert.Same(expected, actual);
        Assert.Empty(context.Calls);
    }

    [Fact]
    public async Task Filters_observe_the_pipeline_cancellation_token()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var configurator = new PipeConfigurator<TestContext>();
        configurator.UseExecute(context =>
        {
            Assert.Equal(source.Token, context.CancellationToken);
            Assert.True(context.CancellationToken.IsCancellationRequested);
            return Task.CompletedTask;
        });

        await configurator.Build().Send(new TestContext(source.Token));
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

    [Fact]
    public async Task Retry_Filter_Retries_On_Failure()
    {
        var configurator = new PipeConfigurator<TestContext>();
        var attempts = 0;
        configurator.UseRetry(2);
        configurator.UseExecute((ctx) =>
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
        configurator.UseExecute((ctx) =>
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
