using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MyServiceBus.Serialization;
using Npgsql;

namespace MyServiceBus.Persistence.PostgreSql;

public static class PostgreSqlJobExtensions
{
    /// <summary>
    /// Uses PostgreSQL for durable tracked-job storage and embedded execution.
    /// </summary>
    public static IServiceCollection AddBuiltInJobsWithPostgreSql(
        this IServiceCollection services,
        string serviceName,
        Action<PostgreSqlJobOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var options = new PostgreSqlJobOptions();
        configure?.Invoke(options);
        options.Validate();

        services.RemoveAll<IJobProvider>();
        services.RemoveAll<IJobClient>();
        services.RemoveAll<IJobSource>();
        services.AddSingleton(options);
        services.AddSingleton<PostgreSqlJobProcessor>(provider => new PostgreSqlJobProcessor(
            provider.GetRequiredService<NpgsqlDataSource>(),
            serviceName,
            provider.GetRequiredService<IJobConsumerRegistry>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IInboundMessageResolver>(),
            options,
            provider.GetService<TimeProvider>()));
        services.AddSingleton<PostgreSqlJobService>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<PostgreSqlJobService>());
        services.AddSingleton<IJobProvider>(provider => new PostgreSqlJobProvider(
            provider.GetRequiredService<NpgsqlDataSource>(),
            serviceName,
            provider.GetRequiredService<IJobConsumerRegistry>(),
            provider.GetRequiredService<IMessageSerializer>(),
            provider.GetService<TimeProvider>()));
        services.AddSingleton<IJobClient>(provider => provider.GetRequiredService<IJobProvider>());
        services.AddSingleton<IJobSource>(provider => provider.GetRequiredService<IJobProvider>());
        return services;
    }
}
