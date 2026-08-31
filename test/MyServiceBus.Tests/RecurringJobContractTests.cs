namespace MyServiceBus.Tests;

public class RecurringJobContractTests
{
    [Fact]
    public void Identity_normalizes_caller_supplied_values()
    {
        var identity = new RecurringJobIdentity(" daily-export ", " billing ");

        Assert.Equal("daily-export", identity.ScheduleId);
        Assert.Equal("billing", identity.ScheduleGroup);
        Assert.Equal(new RecurringJobIdentity("daily-export", "billing"), identity);
    }

    [Fact]
    public void Identity_rejects_a_blank_schedule_id()
    {
        Assert.Throws<ArgumentException>(() => new RecurringJobIdentity("  "));
    }

    [Fact]
    public void Fixed_interval_requires_a_positive_duration_and_normalizes_anchor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedIntervalRecurringJobCadence(TimeSpan.Zero));

        var cadence = new FixedIntervalRecurringJobCadence(
            TimeSpan.FromMinutes(15),
            new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.FromHours(2)));

        Assert.Equal(TimeSpan.FromMinutes(15), cadence.Interval);
        Assert.Equal(DateTimeOffset.Parse("2026-09-01T01:00:00Z"), cadence.AnchorAtUtc);
    }

    [Fact]
    public void Cron_requires_an_explicit_dialect_and_defaults_to_utc()
    {
        var cadence = new CronRecurringJobCadence(" 0 1 * * * ", RecurringJobCronDialect.Unix5);

        Assert.Equal("0 1 * * *", cadence.Expression);
        Assert.Equal(RecurringJobCronDialect.Unix5, cadence.Dialect);
        Assert.Equal("UTC", cadence.TimeZoneId);
        Assert.Throws<ArgumentException>(() => new CronRecurringJobCadence(
            "0 1 * * *",
            (RecurringJobCronDialect)999));
    }

    [Fact]
    public void Definition_has_safe_mvp_defaults()
    {
        var definition = new RecurringJobDefinition(
            new RecurringJobIdentity("daily-export"),
            new FixedIntervalRecurringJobCadence(TimeSpan.FromDays(1)));

        Assert.Equal(RecurringJobMisfirePolicy.FireOnceNow, definition.MisfirePolicy);
        Assert.Equal(1, definition.MaxCatchUpOccurrences);
        Assert.Equal(RecurringJobOverlapPolicy.Allow, definition.OverlapPolicy);
    }

    [Fact]
    public void Definition_rejects_an_invalid_window_or_catch_up_cap()
    {
        var identity = new RecurringJobIdentity("daily-export");
        var cadence = new FixedIntervalRecurringJobCadence(TimeSpan.FromDays(1));
        var start = DateTimeOffset.Parse("2026-09-02T00:00:00Z");

        Assert.Throws<ArgumentException>(() => new RecurringJobDefinition(
            identity,
            cadence,
            startAtUtc: start,
            endAtUtc: start));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecurringJobDefinition(
            identity,
            cadence,
            maxCatchUpOccurrences: 0));
        Assert.Throws<ArgumentException>(() => new RecurringJobDefinition(
            identity,
            cadence,
            misfirePolicy: (RecurringJobMisfirePolicy)999));
    }
}
