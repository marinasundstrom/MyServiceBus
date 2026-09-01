using Microsoft.Extensions.Options;

namespace MyServiceBus.Monitoring.Server;

public enum MonitoringMessageBodyDisclosure
{
    Omit,
    Redact,
    Full
}

public sealed class MonitoringDisclosureOptions
{
    public const string SectionName = "Monitoring:Disclosure";

    public MonitoringMessageBodyDisclosure MessageBodies { get; set; } = MonitoringMessageBodyDisclosure.Omit;
    public string MessageBodyRedactionText { get; set; } = "[redacted]";
}

public sealed class MonitoringDisclosurePolicy
{
    private readonly MonitoringDisclosureOptions options;

    public MonitoringDisclosurePolicy(IOptions<MonitoringDisclosureOptions> options)
    {
        this.options = options.Value;
    }

    public IReadOnlyList<MonitoringObservationRecord> Apply(IReadOnlyList<MonitoringObservationRecord> records)
        => options.MessageBodies == MonitoringMessageBodyDisclosure.Full
            ? records
            : records.Select(Apply).ToArray();

    public MonitoringMessageSummary Apply(MonitoringMessageSummary message)
        => options.MessageBodies switch
        {
            MonitoringMessageBodyDisclosure.Full => message,
            MonitoringMessageBodyDisclosure.Redact when message.MessageBodyStatus is "captured" or "truncated"
                => message with { MessageBodyStatus = "redacted" },
            MonitoringMessageBodyDisclosure.Omit when message.MessageBodyStatus is "captured" or "truncated"
                => message with { MessageBodyStatus = "withheld" },
            _ => message
        };

    private MonitoringObservationRecord Apply(MonitoringObservationRecord record)
    {
        var observation = record.Observation;
        if (observation.MessageBody is null)
            return record;

        var disclosed = options.MessageBodies switch
        {
            MonitoringMessageBodyDisclosure.Redact => observation with
            {
                MessageBody = observation.MessageBody is null ? null : options.MessageBodyRedactionText,
                MessageBodyContentType = observation.MessageBody is null ? null : "text/plain",
                MessageBodyStatus = "redacted",
                MessageBodyOriginalBytes = null
            },
            _ => observation with
            {
                MessageBody = null,
                MessageBodyContentType = null,
                MessageBodyStatus = "withheld",
                MessageBodyOriginalBytes = null
            }
        };

        return record with { Observation = disclosed };
    }
}
