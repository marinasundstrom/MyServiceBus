using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IHttpFactoryConfigurator
{
    Uri BaseAddress { get; }
    IEndpointNameFormatter? EndpointNameFormatter { get; }
    void SetEndpointNameFormatter(IEndpointNameFormatter formatter);
    void ReceiveEndpoint(string path, Action<HttpReceiveEndpointConfigurator> configure);
}

internal sealed class HttpFactoryConfigurator : IHttpFactoryConfigurator
{
    private readonly IList<Action<IMessageBus, IServiceProvider>> _endpointActions = new List<Action<IMessageBus, IServiceProvider>>();
    private IEndpointNameFormatter? _endpointNameFormatter;

    public HttpFactoryConfigurator(Uri baseAddress)
    {
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }
    public IEndpointNameFormatter? EndpointNameFormatter => _endpointNameFormatter;

    public void SetEndpointNameFormatter(IEndpointNameFormatter formatter)
    {
        _endpointNameFormatter = formatter;
    }

    public void ReceiveEndpoint(string path, Action<HttpReceiveEndpointConfigurator> configure)
    {
        var configurator = new HttpReceiveEndpointConfigurator(BaseAddress, path, _endpointActions);
        configure(configurator);
    }

    internal void Apply(IMessageBus bus, IServiceProvider provider)
    {
        foreach (var action in _endpointActions)
            action(bus, provider);
    }
}

public class HttpReceiveEndpointConfigurator
{
    private readonly Uri _baseAddress;
    private readonly string _path;
    private readonly IList<Action<IMessageBus, IServiceProvider>> _endpointActions;

    public HttpReceiveEndpointConfigurator(Uri baseAddress, string path, IList<Action<IMessageBus, IServiceProvider>> endpointActions)
    {
        _baseAddress = baseAddress;
        _path = path;
        _endpointActions = endpointActions;
    }

    [Throws(typeof(InvalidOperationException))]
    public void ConfigureConsumer<T>(IBusRegistrationContext context)
    {
        var consumerType = typeof(T);

        try
        {
            var registry = context.ServiceProvider.GetRequiredService<TopologyRegistry>();
            var consumer = registry.Consumers.First(c => c.ConsumerType == consumerType);
            consumer.Address = new Uri(_baseAddress, _path).ToString();

            foreach (var binding in consumer.Bindings)
                binding.EntityName = EntityNameFormatter.Format(binding.MessageType);

            var messageType = consumer.Bindings.First().MessageType;
            var method = typeof(IMessageBus).GetMethod(nameof(IMessageBus.AddConsumer))!
                .MakeGenericMethod(messageType, consumerType);

            _endpointActions.Add([Throws(typeof(TargetException), typeof(TargetInvocationException))] (bus, _) =>
                ((Task)method.Invoke(bus, new object[] { consumer, consumer.ConfigurePipe, CancellationToken.None })!)
                    .GetAwaiter().GetResult());
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException($"Failed to configure consumer {consumerType.Name}", ex.InnerException);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to configure consumer {consumerType.Name}", ex);
        }
    }
}
