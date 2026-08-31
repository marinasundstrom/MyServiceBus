using Shouldly;

namespace MyServiceBus.Tests;

public class JobContractTests
{
    [Fact]
    public void Job_consumer_uses_the_specialized_marker()
    {
        typeof(IJobConsumer).IsAssignableFrom(typeof(TestJobConsumer)).ShouldBeTrue();
        typeof(IConsumer<TestJob>).IsAssignableFrom(typeof(TestJobConsumer)).ShouldBeFalse();
    }

    [Fact]
    public void Submission_options_reject_an_empty_identifier()
    {
        Should.Throw<ArgumentException>(() => new JobSubmissionOptions(Guid.Empty));
        Should.Throw<ArgumentException>(() => new JobSubmissionOptions(recurringJobOccurrenceId: Guid.Empty));
    }

    [Fact]
    public void Progress_validates_its_range()
    {
        new JobProgress(2, 10).ShouldBe(new JobProgress(2, 10));
        Should.Throw<ArgumentOutOfRangeException>(() => new JobProgress(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new JobProgress(1, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new JobProgress(11, 10));
    }

    private sealed record TestJob;

    private sealed class TestJobConsumer : IJobConsumer<TestJob>
    {
        public Task Run(JobContext<TestJob> context) => Task.CompletedTask;
    }
}
