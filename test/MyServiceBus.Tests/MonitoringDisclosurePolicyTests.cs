using Microsoft.Extensions.Options;
using MyServiceBus.Monitoring;
using MyServiceBus.Monitoring.Server;
using Shouldly;

public class MonitoringDisclosurePolicyTests
{
    [Fact]
    public void Omit_is_the_default_and_does_not_mutate_the_retained_observation()
    {
        var retained = CreateRecord();
        var policy = CreatePolicy(new MonitoringDisclosureOptions());

        var disclosed = policy.Apply([retained]).ShouldHaveSingleItem();

        disclosed.Observation.MessageBody.ShouldBeNull();
        disclosed.Observation.MessageBodyStatus.ShouldBe("withheld");
        disclosed.Observation.MessageBodyOriginalBytes.ShouldBeNull();
        retained.Observation.MessageBody.ShouldBe("{\"secret\":\"value\"}");
        retained.Observation.MessageBodyStatus.ShouldBe("captured");
    }

    [Fact]
    public void Redact_replaces_the_whole_body_and_removes_size_metadata()
    {
        var policy = CreatePolicy(new MonitoringDisclosureOptions
        {
            MessageBodies = MonitoringMessageBodyDisclosure.Redact,
            MessageBodyRedactionText = "not disclosed"
        });

        var disclosed = policy.Apply([CreateRecord()]).ShouldHaveSingleItem().Observation;

        disclosed.MessageBody.ShouldBe("not disclosed");
        disclosed.MessageBodyContentType.ShouldBe("text/plain");
        disclosed.MessageBodyStatus.ShouldBe("redacted");
        disclosed.MessageBodyOriginalBytes.ShouldBeNull();
    }

    [Fact]
    public void Full_returns_the_retained_records_unchanged()
    {
        var retained = new[] { CreateRecord() };
        var policy = CreatePolicy(new MonitoringDisclosureOptions
        {
            MessageBodies = MonitoringMessageBodyDisclosure.Full
        });

        policy.Apply(retained).ShouldBeSameAs(retained);
    }

    private static MonitoringDisclosurePolicy CreatePolicy(MonitoringDisclosureOptions options)
        => new(Options.Create(options));

    private static MonitoringObservationRecord CreateRecord()
        => new(
            "orders",
            "orders-1",
            "bus",
            new MonitoringObservation(
                1,
                DateTimeOffset.UtcNow,
                "consumed",
                true,
                "SubmitOrder",
                "urn:message:SubmitOrder",
                "orders",
                null,
                5,
                null,
                null,
                null,
                null,
                null,
                null,
                MessageBody: "{\"secret\":\"value\"}",
                MessageBodyContentType: "application/json",
                MessageBodyStatus: "captured",
                MessageBodyOriginalBytes: 18));
}
