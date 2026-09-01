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
