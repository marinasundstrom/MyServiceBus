import Link from 'next/link';
import LanguageTabs from '../../components/LanguageTabs';

const install = {
  csharp: `dotnet add package Sundstrom.MyServiceBus.RabbitMq \\
  --version 0.1.0-preview.4`,
  java: `dependencies {
    implementation 'io.github.marinasundstrom.myservicebus:myservicebus-rabbitmq:0.1.0-preview.4'
}`,
};

const configure = {
  csharp: `var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost");
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();
await app.StartAsync();`,
  java: `ServiceCollection services = ServiceCollection.create();

services.from(MessageBusServices.class)
    .addServiceBus(cfg -> {
        cfg.addConsumer(SubmitOrderConsumer.class);
        cfg.using(RabbitMqFactoryConfigurator.class,
            (context, rabbit) -> {
                rabbit.host("localhost");
                rabbit.configureEndpoints(context);
            });
    });

ServiceProvider provider = services.buildServiceProvider();
MessageBus bus = provider.getService(MessageBus.class);
bus.start().join();`,
};

const messages = {
  csharp: `public record SubmitOrder(Guid OrderId);
public record OrderSubmitted(Guid OrderId);

public class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context) =>
        context.Publish(new OrderSubmitted(context.Message.OrderId));
}`,
  java: `public record SubmitOrder(UUID orderId) { }
public record OrderSubmitted(UUID orderId) { }

public class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
        return context.publish(
            new OrderSubmitted(context.getMessage().orderId()));
    }
}`,
};

const publish = {
  csharp: `var bus = app.Services.GetRequiredService<IMessageBus>();
await bus.Publish(new SubmitOrder(Guid.NewGuid()));`,
  java: `MessageBus bus = provider.getService(MessageBus.class);
bus.publish(new SubmitOrder(UUID.randomUUID())).join();`,
};

export default function GettingStarted() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Getting started</p>
      <h1>Publish your first message.</h1>
      <p className="docs-summary">
        In four short steps, configure RabbitMQ, register a consumer, and publish
        an event. Choose your language at each step.
      </p>

      <ol className="step-list">
        <li>
          <div className="step-heading"><span>01</span><div><h2>Install the RabbitMQ transport</h2><p>The transport brings in the core runtime and abstractions.</p></div></div>
          <LanguageTabs csharp={install.csharp} java={install.java} csharpLabel=".NET CLI" javaLabel="Gradle" csharpLanguage="shell" javaLanguage="groovy" />
          <p className="small-note">Using Maven? Add the same group, artifact, and version as a standard dependency.</p>
        </li>
        <li>
          <div className="step-heading"><span>02</span><div><h2>Configure the bus</h2><p>Register the consumer and let MyServiceBus create its receive endpoint.</p></div></div>
          <LanguageTabs csharp={configure.csharp} java={configure.java} />
        </li>
        <li>
          <div className="step-heading"><span>03</span><div><h2>Define messages and a consumer</h2><p>A consumer handles one message type through a consume context.</p></div></div>
          <LanguageTabs csharp={messages.csharp} java={messages.java} />
        </li>
        <li>
          <div className="step-heading"><span>04</span><div><h2>Publish</h2><p>Publishing fans the event out to every interested receive endpoint.</p></div></div>
          <LanguageTabs csharp={publish.csharp} java={publish.java} />
        </li>
      </ol>

      <div className="callout">
        <strong>RabbitMQ must be running</strong>
        <p>The examples connect to a broker on <code>localhost</code> using its default connection settings.</p>
      </div>

      <div className="next-card"><div><span>Next</span><strong>Understand the messaging model</strong></div><Link href="/docs/concepts">Core concepts →</Link></div>
    </article>
  );
}
