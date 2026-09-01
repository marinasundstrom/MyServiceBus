using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Choreography;
using MyServiceBus.Orchestration;

namespace MyServiceBus;

public interface IBusRegistrationConfigurator : IRegistrationConfigurator
{
    IServiceCollection Services { get; }

    void AddChoreography(ChoreographyFragment fragment);

    /// <summary>
    /// Registers an in-memory saga state machine and all of its declared event contracts on one endpoint.
    /// </summary>
    void AddSagaStateMachine<TStateMachine, TSaga>(
        TStateMachine stateMachine,
        string? endpointName = null)
        where TStateMachine : SagaStateMachine<TSaga>
        where TSaga : class;

    /// <summary>
    /// Constructs and registers an in-memory saga state machine with a public parameterless constructor.
    /// </summary>
    void AddSagaStateMachine<TStateMachine, TSaga>(string? endpointName = null)
        where TStateMachine : SagaStateMachine<TSaga>, new()
        where TSaga : class
        => AddSagaStateMachine<TStateMachine, TSaga>(new TStateMachine(), endpointName);

    /// <summary>
    /// Builds and registers a choreography fragment from an existing builder.
    /// </summary>
    void AddChoreography(ChoreographyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddChoreography(builder.Build());
    }

    /// <summary>
    /// Builds and registers one application-owned choreography fragment.
    /// </summary>
    void AddChoreography(
        string choreographyId,
        string definitionVersion,
        string owner,
        Action<ChoreographyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ChoreographyBuilder(choreographyId, definitionVersion, owner);
        configure(builder);
        AddChoreography(builder);
    }

    void AddHook<THook>() where THook : class, IBusHook;
}
