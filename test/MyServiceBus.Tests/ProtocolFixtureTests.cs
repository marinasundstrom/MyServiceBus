using System.Text.Json;

namespace MyServiceBus.Tests;

public class ProtocolFixtureTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Manifest_lists_valid_envelope_fixtures()
    {
        var manifest = Read<FixtureManifest>("manifest.json");

        Assert.Equal("1", manifest.ProtocolVersion);
        Assert.Equal("application/vnd.masstransit+json", manifest.ContentType);
        Assert.Equal(["message", "request", "fault"], manifest.Fixtures.Select(x => x.Kind));

        foreach (var fixture in manifest.Fixtures)
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(FixturePath(fixture.File)));
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
            Assert.True(document.RootElement.TryGetProperty("messageId", out _));
            Assert.True(document.RootElement.TryGetProperty("messageType", out _));
            Assert.True(document.RootElement.TryGetProperty("message", out _));
        }
    }

    [Fact]
    public void Message_fixture_deserializes_with_portable_metadata()
    {
        var envelope = Read<Envelope<Dictionary<string, JsonElement>>>("message-envelope.json");

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), envelope.MessageId);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), envelope.CorrelationId);
        Assert.Equal("urn:message:MyServiceBus.Compatibility:SubmitOrder", Assert.Single(envelope.MessageType));
        Assert.Equal("C-123", envelope.Message["customerNumber"].GetString());
        Assert.Equal("tenant-a", ((JsonElement)envelope.Headers["tenant-id"]).GetString());
        Assert.Equal("application/json", envelope.ContentType);
    }

    [Fact]
    public void Request_fixture_deserializes_request_addresses_and_identifiers()
    {
        var envelope = Read<Envelope<Dictionary<string, JsonElement>>>("request-envelope.json");

        Assert.Equal(Guid.Parse("66666666-6666-6666-6666-666666666666"), envelope.RequestId);
        Assert.Equal(new Uri("rabbitmq://localhost/order-api_bus_abc123"), envelope.ResponseAddress);
        Assert.Equal(envelope.ResponseAddress, envelope.FaultAddress);
        Assert.NotNull(envelope.ExpirationTime);
    }

    [Fact]
    public void Fault_fixture_deserializes_typed_fault_contract()
    {
        var envelope = Read<Envelope<Fault<Dictionary<string, JsonElement>>>>("fault-envelope.json");

        Assert.Equal("urn:message:MassTransit:Fault[[MyServiceBus.Compatibility:GetOrderStatus]]", Assert.Single(envelope.MessageType));
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), envelope.Message.FaultId);
        Assert.Equal("Order was not found", Assert.Single(envelope.Message.Exceptions).Message);
        Assert.Equal(
            "44444444-4444-4444-4444-444444444444",
            envelope.Message.Message["orderId"].GetString());
    }

    private static T Read<T>(string fileName)
        where T : class
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllBytes(FixturePath(fileName)), Options)
            ?? throw new InvalidOperationException($"Fixture '{fileName}' deserialized to null.");
    }

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "protocol-fixtures", fileName);

    private sealed class FixtureManifest
    {
        public string ProtocolVersion { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public List<FixtureEntry> Fixtures { get; set; } = [];
    }

    private sealed class FixtureEntry
    {
        public string Name { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }
}
