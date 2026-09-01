using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Orchestration;
using Npgsql;

namespace MyServiceBus.Persistence.PostgreSql;

public static class PostgreSqlSagaExtensions
{
    /// <summary>
    /// Registers a state machine with durable PostgreSQL saga storage and a transactional outbox.
    /// </summary>
    public static void AddPostgreSqlSagaStateMachine<TStateMachine, TSaga>(
        this IBusRegistrationConfigurator configurator,
        TStateMachine stateMachine,
        string serviceName,
        string? sagaType = null,
        string? endpointName = null)
        where TStateMachine : SagaStateMachine<TSaga>
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(stateMachine);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        configurator.AddSagaStateMachine(
            stateMachine,
            PostgreSqlSagaRepository<TSaga>.ProviderCapabilities,
            provider => new PostgreSqlSagaRepository<TSaga>(
                provider.GetRequiredService<NpgsqlDataSource>(),
                provider.GetRequiredService<OutboxSession>(),
                serviceName,
                sagaType),
            endpointName);
    }
}
