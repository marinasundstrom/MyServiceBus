using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Serialization;

namespace MyServiceBus.Tests;

public class SerializerFactoryRegistrationTests
{
    [Fact]
    public void Serializer_factory_resolves_application_services_without_reflective_activation()
    {
        var services = new ServiceCollection();
        var dependency = new SerializerDependency();
        services.AddSingleton(dependency);
        var configurator = new BusRegistrationConfigurator(services);

        configurator.SetSerializer(provider =>
            new FactorySerializer(provider.GetRequiredService<SerializerDependency>()));
        configurator.Build();

        using var provider = services.BuildServiceProvider();
        var serializer = Assert.IsType<FactorySerializer>(
            provider.GetRequiredService<IMessageSerializer>());
        Assert.Same(dependency, serializer.Dependency);
    }

    private sealed class SerializerDependency
    {
    }

    private sealed class FactorySerializer(SerializerDependency dependency) : IMessageSerializer
    {
        public SerializerDependency Dependency { get; } = dependency;

        public string ContentType => "application/factory-test";

        public MessageEnvelopeMode EnvelopeMode => MessageEnvelopeMode.Raw;

        public Task<byte[]> SerializeAsync<T>(MessageSerializationContext<T> context)
            where T : class => Task.FromResult(Array.Empty<byte>());
    }
}
