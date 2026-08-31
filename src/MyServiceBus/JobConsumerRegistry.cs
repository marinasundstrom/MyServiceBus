using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

internal interface IJobConsumerDescriptor
{
    Type JobType { get; }

    string JobTypeName { get; }

    JobConsumerOptions Options { get; }

    SemaphoreSlim Concurrency { get; }

    Task Run(IServiceProvider serviceProvider, JobExecutionContext context);
}

internal sealed class JobConsumerDescriptor<TConsumer, TJob> : IJobConsumerDescriptor
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

internal sealed class JobConsumerRegistry
{
    private readonly Dictionary<Type, IJobConsumerDescriptor> descriptors = [];

    public void Add<TConsumer, TJob>(JobConsumerOptions options)
        where TConsumer : class, IJobConsumer<TJob>
        where TJob : class
    {
        if (!descriptors.TryAdd(typeof(TJob), new JobConsumerDescriptor<TConsumer, TJob>(options)))
            throw new InvalidOperationException($"A job consumer is already registered for {typeof(TJob)}.");
    }

    public IJobConsumerDescriptor Get(Type jobType) =>
        descriptors.TryGetValue(jobType, out var descriptor)
            ? descriptor
            : throw new InvalidOperationException($"No job consumer is registered for {jobType}.");
}
