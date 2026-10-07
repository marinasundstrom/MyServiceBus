using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyServiceBus.Serialization;

internal sealed class InterfaceMessageConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsInterface && MessageTypeCache.IsContractType(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert))!;

    private sealed class Converter<T> : JsonConverter<T> where T : class
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("An interface message must be a JSON object.");
            var proxy = DispatchProxy.Create<T, PropertyProxy>();
            var values = ((PropertyProxy)(object)proxy).Values;
            foreach (var property in Properties())
            {
                var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
                var found = document.RootElement.EnumerateObject().FirstOrDefault(p =>
                    string.Equals(p.Name, name, options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
                values[property.Name] = found.Value.ValueKind == JsonValueKind.Undefined
                    ? (property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null)
                    : found.Value.Deserialize(property.PropertyType, options);
            }
            return proxy;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var property in Properties())
            {
                writer.WritePropertyName(property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name);
                JsonSerializer.Serialize(writer, property.GetValue(value), property.PropertyType, options);
            }
            writer.WriteEndObject();
        }

        private static IEnumerable<PropertyInfo> Properties() => typeof(T).GetInterfaces().Append(typeof(T))
            .SelectMany(t => t.GetProperties()).DistinctBy(p => p.Name);
    }

    public class PropertyProxy : DispatchProxy
    {
        public Dictionary<string, object?> Values { get; } = new();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name.StartsWith("get_")) return Values.GetValueOrDefault(targetMethod.Name[4..]);
            if (targetMethod.Name.StartsWith("set_")) { Values[targetMethod.Name[4..]] = args![0]; return null; }
            throw new NotSupportedException("Message interfaces may only declare properties.");
        }
    }
}
