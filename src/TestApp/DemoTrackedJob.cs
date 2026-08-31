using MyServiceBus;

namespace TestApp;

public sealed record DemoTrackedJob(
    string ReportName,
    bool FailFirstAttempt,
    bool FailAlways);

public sealed class DemoTrackedJobConsumer : IJobConsumer<DemoTrackedJob>
{
    public async Task Run(JobContext<DemoTrackedJob> context)
    {
        if (context.Job.FailAlways || context.Job.FailFirstAttempt && context.RetryAttempt == 0)
            throw new InvalidOperationException("The sample report job was asked to demonstrate a failed attempt.");

        for (var step = 1; step <= 3; step++)
        {
            await context.SetProgress(step, 3, context.CancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(250), context.CancellationToken);
        }
    }
}
