using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MyServiceBus;

public static class RabbitMqHealthCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddMyServiceBus(this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<RabbitMqHealthCheck>("myservicebus");
    }
}

