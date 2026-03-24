using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus;
using OpenTelemetry.Trace;
using MyServiceBus.Serialization;

public class ServiceDefaultsOpenTelemetryTests
{
    class TestMessage { }

    [Fact]
    public async Task AddServiceDefaults_registers_myservicebus_activity_source()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();

        using var host = builder.Build();
        _ = host.Services.GetRequiredService<TracerProvider>();

        var cfg = new PipeConfigurator<SendContext>();
        cfg.UseFilter(new OpenTelemetrySendFilter());
        var pipe = cfg.Build();
        var context = new SendContext(
            MessageTypeCache.GetMessageTypes(typeof(TestMessage)),
            new EnvelopeMessageSerializer());

        await pipe.Send(context);

        Assert.True(context.Headers.ContainsKey(MyServiceBusDiagnostics.TraceParent));
    }
}
