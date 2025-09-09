using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyServiceBus.Topology;
using MyServiceBus.Serialization;
using System;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyServiceBus;

public class BusRegistrationConfigurator : IBusRegistrationConfigurator
{
    private TopologyRegistry _topology = new TopologyRegistry();
    private readonly PipeConfigurator<SendContext> sendConfigurator = new();
    private readonly PipeConfigurator<PublishContext> publishConfigurator = new();
    private Type serializerType = typeof(EnvelopeMessageSerializer);

    public IServiceCollection Services { get; }

    public BusRegistrationConfigurator(IServiceCollection services)
    {
        Services = services;
        sendConfigurator.UseFilter(new OpenTelemetrySendFilter());
        publishConfigurator.UseFilter(new OpenTelemetrySendFilter());
    }

    [Throws(typeof(InvalidOperationException), typeof(TargetInvocationException), typeof(NotSupportedException), typeof(RegexMatchTimeoutException), typeof(AmbiguousMatchException))]
    public void AddConsumer<TConsumer>() where TConsumer : class, IConsumer
    {
        Services.AddScoped<TConsumer>();
        Services.AddScoped<IConsumer, TConsumer>([Throws(typeof(InvalidOperationException))] (sp) => sp.GetRequiredService<TConsumer>());

        var messageType = GetHandledMessageTypes(typeof(TConsumer)).First();

        _topology.RegisterConsumer<TConsumer>(
          queueName: KebabCaseEndpointNameFormatter.Instance.Format(messageType),
          configurePipe: null,
          messageTypes: messageType
      );
    }

    [Throws(typeof(InvalidOperationException), typeof(RegexMatchTimeoutException), typeof(AmbiguousMatchException))]
    public void AddConsumer<TConsumer, TMessage>(Action<PipeConfigurator<ConsumeContext<TMessage>>>? configure = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        Services.AddScoped<TConsumer>();
        Services.AddScoped<IConsumer, TConsumer>([Throws(typeof(InvalidOperationException))] (sp) => sp.GetRequiredService<TConsumer>());

        _topology.RegisterConsumer<TConsumer>(
            queueName: KebabCaseEndpointNameFormatter.Instance.Format(typeof(TMessage)),
            configurePipe: configure,
            messageTypes: typeof(TMessage));
    }

    [Throws(typeof(InvalidOperationException), typeof(TargetInvocationException), typeof(NotSupportedException), typeof(OverflowException), typeof(ReflectionTypeLoadException), typeof(TargetException), typeof(TargetParameterCountException), typeof(MethodAccessException))]
    public void AddConsumers(params Assembly[] assemblies)
    {
        var consumerTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IConsumer).IsAssignableFrom(t)
                        && t.IsClass
                        && !t.IsAbstract
                        && !t.ContainsGenericParameters);

        var method = GetType().GetMethods()
            .First(m => m.Name == nameof(AddConsumer)
                        && m.GetGenericArguments().Length == 1
                        && m.GetParameters().Length == 0);

        foreach (var type in consumerTypes)
        {
            var generic = method.MakeGenericMethod(type);
            generic.Invoke(this, null);
        }
    }

    public void ConfigureSend(Action<PipeConfigurator<SendContext>> configure)
    {
        configure(sendConfigurator);
    }

    public void ConfigurePublish(Action<PipeConfigurator<PublishContext>> configure)
    {
        configure(publishConfigurator);
    }

    public void SetSerializer<TSerializer>() where TSerializer : class, IMessageSerializer
    {
        serializerType = typeof(TSerializer);
    }

    [Throws(typeof(TargetInvocationException), typeof(NotSupportedException), typeof(InvalidOperationException))]
    private static Type[] GetHandledMessageTypes(Type consumerType)
    {
        return consumerType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToArray();
    }

    public void Build()
    {
        if (!Services.Any(d => d.ServiceType == typeof(ILoggerFactory)))
            Services.AddLogging(b => b.AddSimpleConsole());

        Services.AddSingleton(_topology);
        Services.AddSingleton<IBusTopology>(_ => _topology);
        Services.AddSingleton<IPostBuildAction>(_ => new ConsumerRegistrationAction(_topology));
        Services.AddSingleton<ISendPipe>((sp) => new SendPipe(sendConfigurator.Build(sp)));
        Services.AddSingleton<IPublishPipe>((sp) => new PublishPipe(publishConfigurator.Build(sp)));
        Services.AddSingleton(typeof(IMessageSerializer), serializerType);
        Services.AddSingleton<ISendContextFactory, SendContextFactory>();
        Services.AddSingleton<IPublishContextFactory, PublishContextFactory>();
        Services.AddScoped<ConsumeContextProvider>();
        Services.AddScoped<ISendEndpointProvider, SendEndpointProvider>();
        Services.AddScoped<IPublishEndpointProvider, PublishEndpointProvider>();
        Services.AddScoped<IPublishEndpoint>([Throws(typeof(InvalidOperationException))] (sp) => sp.GetRequiredService<IPublishEndpointProvider>().GetPublishEndpoint());
    }
}
