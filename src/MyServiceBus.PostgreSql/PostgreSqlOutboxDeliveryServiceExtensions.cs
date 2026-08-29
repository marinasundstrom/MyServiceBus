using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus.Persistence;
using Npgsql;

namespace MyServiceBus.Persistence.PostgreSql;

public static class PostgreSqlOutboxDeliveryServiceExtensions
{
    /// <summary>
    /// Adds the PostgreSQL store, transport dispatcher, retry policy, and hosted delivery lifecycle for one
    /// logical service partition. An <see cref="NpgsqlDataSource"/> and the bus transport must already be registered.
    /// </summary>
    public static IServiceCollection AddPostgreSqlOutboxDelivery(
        this IServiceCollection services,
        string serviceName,
        Action<OutboxDeliveryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var options = new OutboxDeliveryOptions();
        options.ServiceName = serviceName;
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IOutboxStore>(provider =>
            new PostgreSqlOutboxStore(provider.GetRequiredService<NpgsqlDataSource>(), serviceName));
        services.AddSingleton(provider =>
            new PostgreSqlOutboxHealth(provider.GetRequiredService<NpgsqlDataSource>(), serviceName));
        services.AddSingleton<IOutboxBacklogProvider>(provider =>
            provider.GetRequiredService<PostgreSqlOutboxHealth>());
        services.AddSingleton<IOutboxTransportDispatcher, TransportOutboxDispatcher>();
        services.AddSingleton<IOutboxRetryPolicy>(_ =>
            new ExponentialOutboxRetryPolicy(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1)));
        services.AddSingleton<OutboxDispatcher>();
        services.AddSingleton<OutboxDeliveryService>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<OutboxDeliveryService>());
        return services;
    }
}
