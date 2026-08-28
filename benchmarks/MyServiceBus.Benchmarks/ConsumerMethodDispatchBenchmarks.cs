using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Generated;
using MyServiceBus.Topology;

namespace MyServiceBus.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class ConsumerMethodDispatchBenchmarks
{
    private static int callCount;

    public sealed record MethodMessage(string Value);

    public sealed class MethodConsumers
    {
        private MethodConsumers()
        {
        }

        [Consumer("benchmark-method")]
        public static Task Consume(MethodMessage message)
        {
            Interlocked.Increment(ref callCount);
            return Task.CompletedTask;
        }
    }

    private ServiceProvider generatedProvider = null!;
    private IConsumer<MethodMessage> generatedConsumer = null!;
    private DefaultConsumeContext<MethodMessage> context = null!;

    [GlobalSetup]
    public void Setup()
    {
        generatedProvider = BuildProvider(configurator => configurator.AddGeneratedConsumers());
        generatedConsumer = ResolveMethodConsumer(generatedProvider);
        context = new DefaultConsumeContext<MethodMessage>(new MethodMessage("benchmark"));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        generatedProvider.Dispose();
    }

    [Benchmark]
    public Task GeneratedDirectInvocation() => generatedConsumer.Consume(context);

    private static ServiceProvider BuildProvider(Action<BusRegistrationConfigurator> register)
    {
        var services = new ServiceCollection();
        var configurator = new BusRegistrationConfigurator(services);
        register(configurator);
        configurator.Build();
        return services.BuildServiceProvider();
    }

    private static IConsumer<MethodMessage> ResolveMethodConsumer(ServiceProvider provider)
    {
        var topology = provider.GetRequiredService<TopologyRegistry>();
        var consumerType = topology.Consumers.Single(consumer => consumer.QueueName == "benchmark-method").ConsumerType;
        return (IConsumer<MethodMessage>)provider.GetRequiredService(consumerType);
    }
}
