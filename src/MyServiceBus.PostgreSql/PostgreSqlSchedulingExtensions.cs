using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyServiceBus.Persistence;
using Npgsql;

namespace MyServiceBus.Persistence.PostgreSql;

public static class PostgreSqlSchedulingExtensions
{
    /// <summary>
    /// Uses the current PostgreSQL outbox transaction for durable message scheduling.
    /// </summary>
    public static IServiceCollection AddPostgreSqlMessageScheduler(
        this IServiceCollection services,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.RemoveAll<IScheduleMessageProvider>();
        services.RemoveAll<IScheduledWorkSource>();
        services.AddSingleton<IScheduledWorkSource>(provider => new PostgreSqlScheduledWorkSource(
            provider.GetRequiredService<NpgsqlDataSource>(),
            serviceName));
        services.AddScoped<IScheduleMessageProvider>(provider => new PostgreSqlScheduleMessageProvider(
            provider.GetRequiredService<NpgsqlDataSource>(),
            serviceName,
            provider.GetRequiredService<OutboxSession>(),
            provider.GetRequiredService<IPublishEndpoint>(),
            provider.GetRequiredService<ISendEndpointProvider>(),
            provider.GetService<TimeProvider>()));
        return services;
    }
}
