using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using MyServiceBus.Serialization;

namespace MyServiceBus.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class JsonSerializationBenchmarks
{
    private IMessageSerializer _reflectiveEnvelopeSerializer = null!;
    private IMessageDeserializer _reflectiveEnvelopeDeserializer = null!;
    private IMessageSerializer _generatedEnvelopeSerializer = null!;
    private IMessageDeserializer _generatedEnvelopeDeserializer = null!;
    private IMessageSerializer _reflectiveRawSerializer = null!;
    private IMessageDeserializer _reflectiveRawDeserializer = null!;
    private IMessageSerializer _generatedRawSerializer = null!;
    private IMessageDeserializer _generatedRawDeserializer = null!;
    private MessageSerializationContext<JsonBenchmarkMessage> _reflectiveEnvelopeContext = null!;
    private MessageSerializationContext<JsonBenchmarkMessage> _generatedEnvelopeContext = null!;
    private MessageSerializationContext<JsonBenchmarkMessage> _reflectiveRawContext = null!;
    private MessageSerializationContext<JsonBenchmarkMessage> _generatedRawContext = null!;
    private MessageBody _reflectiveEnvelopeBody = null!;
    private MessageBody _generatedEnvelopeBody = null!;
    private MessageBody _reflectiveRawBody = null!;
    private MessageBody _generatedRawBody = null!;

    [GlobalSetup]
    public void Setup()
    {
        var reflectiveEnvelope = new EnvelopeSerializerFactory();
        var generatedEnvelope = new EnvelopeSerializerFactory(JsonBenchmarkContext.Default.Options);
        var reflectiveRaw = new RawJsonSerializerFactory();
        var generatedRaw = new RawJsonSerializerFactory(JsonBenchmarkContext.Default.Options);

        _reflectiveEnvelopeSerializer = reflectiveEnvelope.CreateSerializer();
        _reflectiveEnvelopeDeserializer = reflectiveEnvelope.CreateDeserializer();
        _generatedEnvelopeSerializer = generatedEnvelope.CreateSerializer();
        _generatedEnvelopeDeserializer = generatedEnvelope.CreateDeserializer();
        _reflectiveRawSerializer = reflectiveRaw.CreateSerializer();
        _reflectiveRawDeserializer = reflectiveRaw.CreateDeserializer();
        _generatedRawSerializer = generatedRaw.CreateSerializer();
        _generatedRawDeserializer = generatedRaw.CreateDeserializer();

        _reflectiveEnvelopeContext = CreateContext();
        _generatedEnvelopeContext = CreateContext();
        _reflectiveRawContext = CreateContext();
        _generatedRawContext = CreateContext();
        _reflectiveEnvelopeBody = _reflectiveEnvelopeSerializer.GetMessageBody(_reflectiveEnvelopeContext);
        _generatedEnvelopeBody = _generatedEnvelopeSerializer.GetMessageBody(_generatedEnvelopeContext);
        _reflectiveRawBody = _reflectiveRawSerializer.GetMessageBody(_reflectiveRawContext);
        _generatedRawBody = _generatedRawSerializer.GetMessageBody(_generatedRawContext);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Envelope serialize")]
    public MessageBody ReflectiveEnvelopeSerialize()
        => _reflectiveEnvelopeSerializer.GetMessageBody(_reflectiveEnvelopeContext);

    [Benchmark]
    [BenchmarkCategory("Envelope serialize")]
    public MessageBody SourceGeneratedEnvelopeSerialize()
        => _generatedEnvelopeSerializer.GetMessageBody(_generatedEnvelopeContext);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Envelope deserialize")]
    public JsonBenchmarkMessage ReflectiveEnvelopeDeserialize()
        => Deserialize(_reflectiveEnvelopeDeserializer, _reflectiveEnvelopeBody);

    [Benchmark]
    [BenchmarkCategory("Envelope deserialize")]
    public JsonBenchmarkMessage SourceGeneratedEnvelopeDeserialize()
        => Deserialize(_generatedEnvelopeDeserializer, _generatedEnvelopeBody);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Raw serialize")]
    public MessageBody ReflectiveRawSerialize()
        => _reflectiveRawSerializer.GetMessageBody(_reflectiveRawContext);

    [Benchmark]
    [BenchmarkCategory("Raw serialize")]
    public MessageBody SourceGeneratedRawSerialize()
        => _generatedRawSerializer.GetMessageBody(_generatedRawContext);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Raw deserialize")]
    public JsonBenchmarkMessage ReflectiveRawDeserialize()
        => Deserialize(_reflectiveRawDeserializer, _reflectiveRawBody);

    [Benchmark]
    [BenchmarkCategory("Raw deserialize")]
    public JsonBenchmarkMessage SourceGeneratedRawDeserialize()
        => Deserialize(_generatedRawDeserializer, _generatedRawBody);

    private static JsonBenchmarkMessage Deserialize(IMessageDeserializer deserializer, MessageBody body)
    {
        var inbound = deserializer.Deserialize(body, new Dictionary<string, object>());
        return inbound.TryGetMessage<JsonBenchmarkMessage>(out var message)
            ? message
            : throw new InvalidOperationException("The benchmark message could not be deserialized.");
    }

    private static MessageSerializationContext<JsonBenchmarkMessage> CreateContext()
        => new(new JsonBenchmarkMessage
        {
            OrderId = Guid.Parse("729cd292-4242-4f6d-a770-e2f67b71ac23"),
            CustomerNumber = "C-100042",
            Total = 1234.56m,
            Lines =
            [
                new JsonBenchmarkLine { Sku = "SKU-1", Quantity = 2 },
                new JsonBenchmarkLine { Sku = "SKU-2", Quantity = 5 }
            ]
        })
        {
            MessageId = Guid.Parse("36ac2e17-afbd-425d-a223-25f2f2f08f07"),
            ConversationId = Guid.Parse("62c2185c-99f8-470c-bbb0-dc1589dc50bc"),
            MessageType = [MessageUrn.For(typeof(JsonBenchmarkMessage))],
            Headers = new Dictionary<string, object>
            {
                ["tenant"] = "north",
                ["attempt"] = 1
            },
            SentTime = DateTimeOffset.Parse("2026-08-29T12:00:00Z"),
            HostInfo = new HostInfo
            {
                MachineName = "benchmark",
                ProcessName = "benchmark",
                ProcessId = 42,
                Assembly = "MyServiceBus.Benchmarks",
                AssemblyVersion = "1.0.0",
                FrameworkVersion = ".NET",
                MassTransitVersion = "1.0.0",
                OperatingSystemVersion = "benchmark"
            }
        };
}

public sealed class JsonBenchmarkMessage
{
    public Guid OrderId { get; set; }

    public string CustomerNumber { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public List<JsonBenchmarkLine> Lines { get; set; } = [];
}

public sealed class JsonBenchmarkLine
{
    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(JsonBenchmarkMessage))]
internal partial class JsonBenchmarkContext : JsonSerializerContext
{
}
