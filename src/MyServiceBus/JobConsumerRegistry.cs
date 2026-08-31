using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

public interface IRegisteredJobConsumer
{
    Type JobType { get; }

    string JobTypeName { get; }

    JobConsumerOptions Options { get; }

    SemaphoreSlim Concurrency { get; }

    Task Run(IServiceProvider serviceProvider, JobExecutionContext context);
}

internal sealed class JobConsumerDescriptor<TConsumer, TJob> : IRegisteredJobConsumer
    where TConsumer : class, IJobConsumer<TJob>
    where TJob : class
{
    public JobConsumerDescriptor(JobConsumerOptions options)
    {
        Options = options;
        JobTypeName = options.JobTypeName ?? typeof(TJob).Name;
        Concurrency = new SemaphoreSlim(options.ConcurrentJobLimit, options.ConcurrentJobLimit);
    }

    public Type JobType => typeof(TJob);

    public string JobTypeName { get; }

    public JobConsumerOptions Options { get; }

    public SemaphoreSlim Concurrency { get; }

    public Task Run(IServiceProvider serviceProvider, JobExecutionContext context)
    {
        var consumer = serviceProvider.GetRequiredService<TConsumer>();
        return consumer.Run(new InMemoryJobContext<TJob>(context, (TJob)context.Job));
    }
}

public interface IJobConsumerRegistry
{
    IRegisteredJobConsumer Get(Type jobType);

    IRegisteredJobConsumer Get(string jobTypeName);
}

internal sealed class JobConsumerRegistry : IJobConsumerRegistry
{
    private readonly Dictionary<Type, IRegisteredJobConsumer> descriptors = [];
    private readonly Dictionary<string, IRegisteredJobConsumer> descriptorsByName =
        new(StringComparer.Ordinal);

    public void Add<TConsumer, TJob>(JobConsumerOptions options)
        where TConsumer : class, IJobConsumer<TJob>
        where TJob : class
    {
        var descriptor = new JobConsumerDescriptor<TConsumer, TJob>(options);
        if (descriptors.ContainsKey(typeof(TJob)))
            throw new InvalidOperationException($"A job consumer is already registered for {typeof(TJob)}.");
        if (descriptorsByName.ContainsKey(descriptor.JobTypeName))
            throw new InvalidOperationException($"A job consumer is already registered for job type name '{descriptor.JobTypeName}'.");

        descriptors.Add(typeof(TJob), descriptor);
        descriptorsByName.Add(descriptor.JobTypeName, descriptor);
    }

    public IRegisteredJobConsumer Get(Type jobType) =>
        descriptors.TryGetValue(jobType, out var descriptor)
            ? descriptor
            : throw new InvalidOperationException($"No job consumer is registered for {jobType}.");

    public IRegisteredJobConsumer Get(string jobTypeName) =>
        descriptorsByName.TryGetValue(jobTypeName, out var descriptor)
            ? descriptor
            : throw new InvalidOperationException($"No job consumer is registered for job type name '{jobTypeName}'.");
}
