using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;

namespace MyServiceBus.Tests;

public class SerializerFactoryRegistrationTests
{
    [Fact]
    public void Serializer_factory_configures_serializer_and_deserializer_without_reflective_activation()
    {
        var services = new ServiceCollection();
        var dependency = new SerializerDependency();
        var configurator = new BusRegistrationConfigurator(services);

        var factory = new FactorySerializerFactory(dependency);
        configurator.ClearSerialization();
        configurator.AddSerializer(factory, isSerializer: true);
        configurator.AddDeserializer(factory, isDefault: true);
        configurator.Build();

        using var provider = services.BuildServiceProvider();
        var serializer = Assert.IsType<FactorySerializer>(
            provider.GetRequiredService<IMessageSerializer>());
        Assert.Same(dependency, serializer.Dependency);
        var resolver = Assert.IsType<InboundMessageResolver>(
            provider.GetRequiredService<IInboundMessageResolver>());
        Assert.Throws<NotSupportedException>(() => resolver.Resolve(
            new StubTransportMessage([], new Dictionary<string, object>())));
        Assert.Equal(1, dependency.DeserializeCalls);
    }

    private sealed class SerializerDependency
    {
        public int DeserializeCalls { get; set; }
    }

    private sealed class FactorySerializerFactory(SerializerDependency dependency) : ISerializerFactory
    {
        public string ContentType => "application/factory-test";

        public IMessageSerializer CreateSerializer() => new FactorySerializer(dependency);

        public IMessageDeserializer CreateDeserializer() => new FactoryDeserializer(dependency);
    }

    private sealed class FactorySerializer(SerializerDependency dependency) : IMessageSerializer, IMessageSerializerMetadata
    {
        public SerializerDependency Dependency { get; } = dependency;

        public string ContentType => "application/factory-test";

        public MessageEnvelopeMode EnvelopeMode => MessageEnvelopeMode.Raw;

        public MessageBody GetMessageBody<T>(MessageSerializationContext<T> context)
            where T : class => new ByteArrayMessageBody(Array.Empty<byte>());
    }

    private sealed class FactoryDeserializer(SerializerDependency dependency) : IMessageDeserializer
    {
        public string ContentType => "application/factory-test";

        public IInboundMessage Deserialize(MessageBody body, IDictionary<string, object> headers)
        {
            dependency.DeserializeCalls++;
            throw new NotSupportedException();
        }

        public MessageBody GetMessageBody(string text) => new ByteArrayMessageBody([]);
    }

    private sealed record StubTransportMessage(byte[] Payload, IDictionary<string, object> Headers)
        : Transports.ITransportMessage
    {
        public bool IsDurable => true;
    }
}
