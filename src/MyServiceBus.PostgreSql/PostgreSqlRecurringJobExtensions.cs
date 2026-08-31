using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyServiceBus.Serialization;
using Npgsql;

namespace MyServiceBus.Persistence.PostgreSql;

public static class PostgreSqlRecurringJobExtensions
{
    /// <summary>
    /// Uses PostgreSQL storage for the built-in durable recurring-job provider.
    /// </summary>
    public static IServiceCollection AddBuiltInRecurringJobsWithPostgreSql(
        this IServiceCollection services,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.RemoveAll<IRecurringJobProvider>();
        services.AddSingleton<IRecurringJobProvider>(provider => new PostgreSqlRecurringJobProvider(
            provider.GetRequiredService<NpgsqlDataSource>(),
            serviceName,
            provider.GetRequiredService<ITransportFactory>(),
            provider.GetRequiredService<IMessageSerializer>(),
            provider.GetService<TimeProvider>()));
        return services;
    }
}
