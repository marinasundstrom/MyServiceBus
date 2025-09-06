using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus;
using Xunit;

public class FilterDiTests
{
    class Counter
    {
        public int Count;
    }

    class TestFilter : IFilter<ConsumeContext<string>>
    {
        readonly Counter counter;
        public TestFilter(Counter counter)
        {
            this.counter = counter;
        }

        public Task Send(ConsumeContext<string> context, IPipe<ConsumeContext<string>> next)
        {
            counter.Count++;
            return next.Send(context);
        }
    }

    [Fact]
    [Throws(typeof(InvalidOperationException), typeof(MissingMethodException))]
    public async Task Resolves_filter_from_service_provider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Counter>();
        services.AddTransient<TestFilter>();
        var provider = services.BuildServiceProvider();

        var configurator = new PipeConfigurator<ConsumeContext<string>>();
        configurator.UseFilter<TestFilter>();
        var pipe = configurator.Build(provider);

        await pipe.Send(new DefaultConsumeContext<string>("hi"));

        Assert.Equal(1, provider.GetRequiredService<Counter>().Count);
    }
}
