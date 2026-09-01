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

    [Fact]
    public void Exchange_detail_requires_the_caller_to_request_bodies_even_under_full_disclosure()
    {
        var retained = CreateRecord();
        var detail = new MonitoringRequestResponseExchangeDetail(
            new MonitoringRequestResponseExchange(
                "request-1", "requested", "orders", "orders-1", null, null,
                "SubmitOrder", "urn:message:SubmitOrder", "message-1",
                null, null, null, "loopback://response",
                retained.Observation.OccurredAtUtc, retained.Observation.OccurredAtUtc,
                null, null, null, 0, false, "partial"),
            [retained]);
        var policy = CreatePolicy(new MonitoringDisclosureOptions
        {
            MessageBodies = MonitoringMessageBodyDisclosure.Full
        });

        var omitted = policy.Apply(detail, includeMessageBodies: false);
        var disclosed = policy.Apply(detail, includeMessageBodies: true);

        omitted.Observations.ShouldHaveSingleItem().Observation.MessageBody.ShouldBeNull();
        omitted.Observations[0].Observation.MessageBodyStatus.ShouldBe("withheld");
        disclosed.Observations.ShouldBeSameAs(detail.Observations);
        retained.Observation.MessageBody.ShouldBe("{\"secret\":\"value\"}");
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
