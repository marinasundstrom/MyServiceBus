using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Choreography;

namespace MyServiceBus;

public interface IBusRegistrationConfigurator : IRegistrationConfigurator
{
    IServiceCollection Services { get; }

    void AddChoreography(ChoreographyFragment fragment);

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
