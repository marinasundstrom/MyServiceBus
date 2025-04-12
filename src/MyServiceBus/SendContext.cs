using System.Threading.Tasks;
using MyServiceBus.Serialization;

namespace MyServiceBus;

public class SendContext
{
    private readonly IMessageSerializer messageSerializer;

    public SendContext(IMessageSerializer messageSerializer)
    {
        this.messageSerializer = messageSerializer;
    }

    public string MessageId { get; set; }
    public string RoutingKey { get; set; } = ""; // Defaults to empty
    public IDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
    public string? CorrelationId { get; set; }

    public async Task<ReadOnlyMemory<byte>> Serialize<T>(T message)
        where T : class
    {
        var context = new MessageSerializationContext<T>(message)
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = null,
            MessageType = [NamingConventions.GetMessageUrn(typeof(T))],
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