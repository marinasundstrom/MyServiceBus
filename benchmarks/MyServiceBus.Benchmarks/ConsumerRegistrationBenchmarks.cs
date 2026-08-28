using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Generated;

namespace MyServiceBus.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ConsumerRegistrationBenchmarks
{
    public sealed record BenchmarkMessage(string Value);

    public sealed class BenchmarkConsumer : IConsumer<BenchmarkMessage>
    {
        public Task Consume(ConsumeContext<BenchmarkMessage> context) => Task.CompletedTask;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Single consumer")]
    public IServiceCollection ReflectionSingleType()
    {
        var services = new ServiceCollection();
        var configurator = new BusRegistrationConfigurator(services);
        configurator.AddConsumer<BenchmarkConsumer>();
        configurator.Build();
        return services;
    }

    [Benchmark]
    [BenchmarkCategory("Single consumer")]
    public IServiceCollection ExplicitTyped()
    {
        var services = new ServiceCollection();
        var configurator = new BusRegistrationConfigurator(services);
        configurator.AddConsumer<BenchmarkConsumer, BenchmarkMessage>();
        configurator.Build();
        return services;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Application catalog")]
    public IServiceCollection ReflectionCatalog()
    {
        var services = new ServiceCollection();
        var configurator = new BusRegistrationConfigurator(services);
        configurator.AddConsumers(
            static type => type == typeof(BenchmarkConsumer)
                || type == typeof(ConsumerMethodDispatchBenchmarks.MethodConsumers),
            typeof(ConsumerRegistrationBenchmarks).Assembly);
        configurator.Build();
        return services;
    }

    [Benchmark]
    [BenchmarkCategory("Application catalog")]
    public IServiceCollection GeneratedCatalog()
    {
        var services = new ServiceCollection();
        var configurator = new BusRegistrationConfigurator(services);
        configurator.AddGeneratedConsumers();
        configurator.Build();
        return services;
    }
}
