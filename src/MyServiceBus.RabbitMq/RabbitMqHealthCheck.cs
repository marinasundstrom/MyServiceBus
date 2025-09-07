using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly ConnectionProvider connectionProvider;

    public RabbitMqHealthCheck(ConnectionProvider connectionProvider)
    {
        this.connectionProvider = connectionProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await connectionProvider.GetOrCreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (connection != null && connection.IsOpen)
                return HealthCheckResult.Healthy();

            return HealthCheckResult.Unhealthy("RabbitMQ connection is closed");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection is not available", ex);
        }
    }
}

