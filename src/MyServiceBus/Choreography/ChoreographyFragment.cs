using System.Text.Json.Serialization;

namespace MyServiceBus.Choreography;

/// <summary>
/// Describes the choreography reactions owned by one application.
/// </summary>
/// <remarks>
/// A fragment is local topology and monitoring metadata. It is not serialized into
/// application message envelopes and does not execute the declared reactions.
/// </remarks>
public sealed record ChoreographyFragment(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("choreographyId")] string ChoreographyId,
    [property: JsonPropertyName("definitionVersion")] string DefinitionVersion,
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("steps")] IReadOnlyList<ChoreographyStep> Steps)
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Validates the portable declaration independently of registration or execution.
    /// </summary>
    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
            throw new InvalidOperationException($"Unsupported choreography schema version {SchemaVersion}; expected {CurrentSchemaVersion}.");

        Required(ChoreographyId, nameof(ChoreographyId));
        Required(DefinitionVersion, nameof(DefinitionVersion));
        Required(Owner, nameof(Owner));

        if (Steps is null || Steps.Count == 0)
            throw new InvalidOperationException("A choreography fragment must declare at least one step.");
        if (Steps.GroupBy(step => step.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new InvalidOperationException("A choreography fragment cannot contain duplicate step IDs.");

        foreach (var step in Steps)
        {
            Required(step.Id, nameof(ChoreographyStep.Id));
            Required(step.TriggerMessageUrn, nameof(ChoreographyStep.TriggerMessageUrn));
            if (step.OwnerComponent is not null)
                Required(step.OwnerComponent, nameof(ChoreographyStep.OwnerComponent));
            if (step.Outputs is null || step.Outputs.Count == 0)
                throw new InvalidOperationException($"Choreography step '{step.Id}' must declare at least one output or terminal outcome.");

            foreach (var output in step.Outputs)
                ValidateOutput(step.Id, output);
        }
    }

    private static void ValidateOutput(string stepId, ChoreographyOutput output)
    {
        if (!Enum.IsDefined(output.Kind) || !Enum.IsDefined(output.Requirement))
            throw new InvalidOperationException($"Choreography step '{stepId}' contains an unknown output kind or requirement.");
        if (output.MinCount < 0 || output.MaxCount < 0)
            throw new InvalidOperationException($"Choreography step '{stepId}' cannot declare a negative output count.");
        if (output.MinCount > output.MaxCount)
            throw new InvalidOperationException($"Choreography step '{stepId}' has a minimum output count greater than its maximum.");
        if (output.WithinMilliseconds <= 0)
            throw new InvalidOperationException($"Choreography step '{stepId}' must use a positive timing expectation.");

        if (output.Kind == ChoreographyOperationKind.Terminal)
        {
            if (output.MessageUrn is not null || output.Destination is not null ||
                output.MinCount is not null || output.MaxCount is not null || output.WithinMilliseconds is not null)
            {
                throw new InvalidOperationException($"Terminal outcome on choreography step '{stepId}' cannot describe a message, destination, count, or timing expectation.");
            }
            return;
        }

        Required(output.MessageUrn, nameof(ChoreographyOutput.MessageUrn));
        if (output.Kind == ChoreographyOperationKind.Send)
            Required(output.Destination, nameof(ChoreographyOutput.Destination));
        else if (output.Kind is (ChoreographyOperationKind.Publish or ChoreographyOperationKind.Respond) && output.Destination is not null)
            throw new InvalidOperationException($"{output.Kind} outcome on choreography step '{stepId}' cannot declare a destination.");
    }

    private static void Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Choreography field '{field}' cannot be empty or whitespace.");
    }
}

/// <summary>
/// Describes one application-owned reaction to a consumed message.
/// </summary>
public sealed record ChoreographyStep(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("triggerMessageUrn")] string TriggerMessageUrn,
    [property: JsonPropertyName("ownerComponent")] string? OwnerComponent,
    [property: JsonPropertyName("outputs")] IReadOnlyList<ChoreographyOutput> Outputs);

/// <summary>
/// Describes one possible result of a choreography reaction.
/// </summary>
public sealed record ChoreographyOutput(
    [property: JsonPropertyName("kind")] ChoreographyOperationKind Kind,
    [property: JsonPropertyName("messageUrn")] string? MessageUrn,
    [property: JsonPropertyName("destination")] string? Destination,
    [property: JsonPropertyName("requirement")] ChoreographyRequirement Requirement,
    [property: JsonPropertyName("minCount")] int? MinCount,
    [property: JsonPropertyName("maxCount")] int? MaxCount,
    [property: JsonPropertyName("withinMilliseconds")] long? WithinMilliseconds);

[JsonConverter(typeof(JsonStringEnumConverter<ChoreographyOperationKind>))]
public enum ChoreographyOperationKind
{
    [JsonStringEnumMemberName("send")]
    Send,

    [JsonStringEnumMemberName("publish")]
    Publish,

    [JsonStringEnumMemberName("respond")]
    Respond,

    [JsonStringEnumMemberName("schedule")]
    Schedule,

    [JsonStringEnumMemberName("terminal")]
    Terminal
}

[JsonConverter(typeof(JsonStringEnumConverter<ChoreographyRequirement>))]
public enum ChoreographyRequirement
{
    [JsonStringEnumMemberName("informational")]
    Informational,

    [JsonStringEnumMemberName("optional")]
    Optional,

    [JsonStringEnumMemberName("expected")]
    Expected
}
