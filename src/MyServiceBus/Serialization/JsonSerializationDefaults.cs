using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace MyServiceBus.Serialization;

internal static class JsonSerializationDefaults
{
    [RequiresDynamicCode("The managed default JSON resolver may generate serialization metadata at runtime.")]
    [RequiresUnreferencedCode("The managed default JSON resolver requires application message members to be preserved.")]
    public static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = JsonSerializerOptions.Default.TypeInfoResolver,
        WriteIndented = false
    };

    [RequiresDynamicCode("The managed default JSON resolver may generate serialization metadata at runtime.")]
    [RequiresUnreferencedCode("The managed default JSON resolver requires application message members to be preserved.")]
    public static JsonSerializerOptions CreateNServiceBusOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = JsonSerializerOptions.Default.TypeInfoResolver,
        WriteIndented = false
    };
}
