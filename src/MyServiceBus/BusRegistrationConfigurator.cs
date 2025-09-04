using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;
using MyServiceBus.Serialization;
using System;
using System.Reflection;

namespace MyServiceBus;

public class BusRegistrationConfigurator : IBusRegistrationConfigurator
{
    private TopologyRegistry _topology = new TopologyRegistry();
    private readonly PipeConfigurator<SendContext> sendConfigurator = new();
    private readonly PipeConfigurator<SendContext> publishConfigurator = new();
    private Type serializerType = typeof(EnvelopeMessageSerializer);

    public IServiceCollection Services { get; }

    public BusRegistrationConfigurator(IServiceCollection services)
    {
        Services = services;
    }

    [Throws(typeof(InvalidOperationException), typeof(TargetInvocationException))]
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

    [Throws(typeof(InvalidOperationException))]
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

    public void ConfigureSend(Action<PipeConfigurator<SendContext>> configure)
    {
        configure(sendConfigurator);
    }

    public void ConfigurePublish(Action<PipeConfigurator<SendContext>> configure)
    {
        configure(publishConfigurator);
    }

    public void SetSerializer<TSerializer>() where TSerializer : class, IMessageSerializer
    {
        serializerType = typeof(TSerializer);
    }

    [Throws(typeof(TargetInvocationException), typeof(NotSupportedException))]
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
        Services.AddSingleton(_topology);
        Services.AddSingleton<IPostBuildAction>(_ => new ConsumerRegistrationAction(_topology));
        Services.AddSingleton<ISendPipe>(_ => new SendPipe(sendConfigurator.Build()));
        Services.AddSingleton<IPublishPipe>(_ => new PublishPipe(publishConfigurator.Build()));
        Services.AddSingleton(typeof(IMessageSerializer), serializerType);
        Services.AddScoped<ConsumeContextProvider>();
        Services.AddScoped<ISendEndpointProvider, SendEndpointProvider>();
    }
}
