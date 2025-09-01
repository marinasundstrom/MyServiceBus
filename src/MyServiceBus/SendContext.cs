using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class SendContext : BasePipeContext
{
    private readonly Type[] messageTypes;
    private readonly IMessageSerializer messageSerializer;

    public SendContext(Type[] messageTypes, IMessageSerializer messageSerializer, CancellationToken cancellationToken = default)
        : base(cancellationToken)
    {
        this.messageTypes = messageTypes;
        this.messageSerializer = messageSerializer;
    }

    public string MessageId { get; set; }
    public string RoutingKey { get; set; } = ""; // Defaults to empty
    public IDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
    public string? CorrelationId { get; set; }
    public Uri? ResponseAddress { get; set; }
    public Uri? FaultAddress { get; set; }

    public async Task<ReadOnlyMemory<byte>> Serialize<T>(T message)
        where T : class
    {
        var context = new MessageSerializationContext<T>(message)
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = null,
            MessageType = [.. messageTypes.Select(x => NamingConventions.GetMessageUrn(x))],
            ResponseAddress = ResponseAddress,
            Headers = Headers,
            SentTime = DateTimeOffset.Now,
            HostInfo = GetHostInfo<T>(),
        };

        return await messageSerializer.SerializeAsync(context);
    }

    private static HostInfo GetHostInfo<T>() where T : class => new HostInfo
    {
        MachineName = Environment.MachineName,
        ProcessName = Environment.ProcessPath ?? "unknown",
        ProcessId = Environment.ProcessId,
        Assembly = typeof(T).Assembly.GetName().Name ?? "unknown",
        AssemblyVersion = typeof(T).Assembly.GetName().Version?.ToString() ?? "unknown",
        FrameworkVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        MassTransitVersion = "your-custom-version", // replace as needed
        OperatingSystemVersion = Environment.OSVersion.VersionString
    };
}