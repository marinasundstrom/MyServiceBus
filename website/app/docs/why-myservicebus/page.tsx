import Link from 'next/link';
import LanguageTabs from '../../components/LanguageTabs';

const sameModel = {
  csharp: `public class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context) =>
        context.Publish(new OrderSubmitted(context.Message.OrderId));
}`,
  java: `public class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    public CompletableFuture<Void> consume(
            ConsumeContext<SubmitOrder> context) {
        return context.publish(
            new OrderSubmitted(context.getMessage().orderId()));
    }
}`,
};

const chooseTransport = {
  csharp: `builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();

    if (builder.Environment.IsDevelopment())
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host("localhost");
            cfg.ConfigureEndpoints(context);
        });
    else
        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(connectionString);
            cfg.ConfigureEndpoints(context);
        });
});`,
  java: `services.from(MessageBusServices.class).addServiceBus(cfg -> {
    cfg.addConsumer(SubmitOrderConsumer.class);

    if (environment.isDevelopment())
        cfg.using(RabbitMqFactoryConfigurator.class,
            (context, bus) -> {
                bus.host("localhost");
                bus.configureEndpoints(context);
            });
    else
        cfg.using(AzureServiceBusFactoryConfigurator.class,
            (context, bus) -> {
                bus.host(connectionString);
                bus.configureEndpoints(context);
            });
});`,
};

export default function WhyMyServiceBus() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Why MyServiceBus?</p>
      <h1>One messaging model across platforms.</h1>
      <p className="docs-summary">
        The main advantage of MyServiceBus today is cross-platform consistency. Use
        the same family of concepts and closely aligned behavior for asynchronous
        communication in C# and Java.
      </p>

      <div className="callout callout-accent">
        <strong>Write for the platform, design for the system</strong>
        <p>
          C# and Java code stays idiomatic, while contracts, consumers, commands,
          events, requests, retries, and faults retain the same meaning. Teams share
          one mental model instead of maintaining separate messaging architectures.
        </p>
      </div>

      <div className="callout">
        <strong>Project status: evaluate it as a preview</strong>
        <p>
          MyServiceBus is not currently a committed product and does not offer the
          commercial support, maturity, or long-term stability of MassTransit. This
          project is not a recommendation to replace MassTransit in systems that need
          those assurances.
        </p>
      </div>

      <h2 id="shared-vocabulary">Carry the same vocabulary between ecosystems</h2>
      <p>
        MyServiceBus deliberately preserves MassTransit&apos;s most useful structural and
        behavioral ideas. Developers can move between .NET and Java services without
        relearning what publish, send, consume, request, retry, or fault means. The APIs
        remain idiomatic to each language, but the responsibilities, message flow, and
        failure model stay recognizable. Existing MassTransit knowledge therefore
        accelerates adoption, but it supports the main cross-platform goal rather than
        replacing it.
      </p>
      <div className="flow-line" aria-label="Knowledge transfer across messaging platforms">
        <span>MassTransit knowledge</span><b>→</b><span>Shared messaging concepts</span><b>→</b><span>MyServiceBus for C# · Java</span>
      </div>
      <p>
        Compatibility is intentional where it supports familiarity and wire-level
        interoperability. Complete source compatibility and feature parity are not the
        goal; advanced features may be absent, and each platform keeps its natural
        dependency-injection, type-system, and asynchronous programming style.
      </p>

      <h2 id="framework-first">Start framework-first, not broker-first</h2>
      <p>
        Early design questions are usually about communication, not infrastructure.
        Is the message a command or an event? Who may consume it? Does the sender need
        an immediate response? What happens after a duplicate, timeout, or partial
        failure? MyServiceBus lets teams express those decisions through contracts,
        consumers, endpoints, publish and send intent, requests, retries, and faults
        before committing the design to native broker concepts.
      </p>
      <div className="callout">
        <strong>An application layer, not a hidden broker</strong>
        <p>MyServiceBus gives common messaging behavior a consistent home while still exposing transport configuration where broker capabilities matter.</p>
      </div>
      <div className="flow-line" aria-label="Framework-first design sequence">
        <span>Business boundary</span><b>→</b><span>Communication intent</span><b>→</b><span>Failure semantics</span><b>→</b><span>Transport profile</span>
      </div>
      <p>
        The in-memory mediator and testing harness provide a fast place to exercise
        the interaction model. When durability, independent processes, and operational
        scale are needed, select a broker transport based on the required guarantees
        and hosting environment. Application messages and consumers retain the same
        conceptual model while transport configuration supplies the native behavior.
      </p>
      <div className="callout">
        <strong>Transport independence is not transport ignorance</strong>
        <p>Validate topology, ordering, settlement, throughput, security, and failure behavior against the broker you will operate. The framework postpones an infrastructure choice; it does not erase its consequences.</p>
      </div>

      <h2 id="direct-api">What direct broker APIs leave to the application</h2>
      <p>
        A native client is deliberately close to its broker. That is ideal when you
        need maximum control, but a production application must then choose and keep
        conventions for serialization, message identity, type naming, topology,
        acknowledgement, retries, dead-lettering, request correlation, dependency
        injection, tracing, and tests. Those choices multiply when .NET and Java
        services must interoperate.
      </p>
      <div className="concept-comparison">
        <section><span className="tag">BROKER SDK</span><h2>Delivery primitives</h2><p>Channels or clients, native messages, entity declarations, settlement, and every broker-specific feature.</p><strong>Your responsibility:</strong><p>Build and govern the application-level messaging conventions.</p></section>
        <section><span className="tag">MYSERVICEBUS</span><h2>Messaging model</h2><p>Typed consumers, send and publish intent, portable envelopes, endpoint lifecycle, and shared failure behavior.</p><strong>Your responsibility:</strong><p>Design contracts, idempotency, service boundaries, and business recovery.</p></section>
      </div>

      <h2 id="what-you-get">What MyServiceBus brings</h2>
      <div className="docs-feature-grid">
        <div><span>01</span><h3>One mental model</h3><p>Send commands, publish events, consume messages, and request responses through aligned C# and Java concepts.</p></div>
        <div><span>02</span><h3>Portable wire contracts</h3><p>Use shared envelopes, message type URNs, identifiers, headers, and MassTransit-aware conventions across languages.</p></div>
        <div><span>03</span><h3>Managed failure paths</h3><p>Configure retry pipelines and use consistent fault, error, and skipped-message destinations.</p></div>
        <div><span>04</span><h3>Low-ceremony setup</h3><p>Register consumers, choose a transport, configure endpoints, and let the hosted bus manage their lifecycle.</p></div>
        <div><span>05</span><h3>Testable behavior</h3><p>Exercise consumers and message flows with the in-memory mediator and testing harness before using a broker.</p></div>
        <div><span>06</span><h3>Operational context</h3><p>Propagate OpenTelemetry context and observe topology, throughput, retries, and failures through optional monitoring.</p></div>
      </div>

      <h2 id="easy-to-start">Get from an idea to a running message flow quickly</h2>
      <p>
        MyServiceBus is designed to make the common path easy. Define a message contract,
        implement a typed consumer, register it with dependency injection, and select a
        transport. Endpoint discovery, consumer scopes, topology conventions, serialization,
        and bus lifecycle are handled by the framework so application development can start
        with the behavior that matters.
      </p>
      <div className="flow-line" aria-label="Getting started with MyServiceBus">
        <span>Define contract</span><b>→</b><span>Write consumer</span><b>→</b><span>Choose transport</span><b>→</b><span>Send a message</span>
      </div>
      <p>
        Use the in-memory mediator for deliberately local flows and fast tests, or run
        RabbitMQ locally for a realistic broker-backed development loop. The same consumer
        model can then move into the deployed environment. The <Link href="/docs/getting-started">getting-started guide</Link> walks through the complete first flow in both C# and Java.
      </p>

      <h2 id="cross-language">Keep concepts aligned across .NET and Java</h2>
      <p>
        The APIs are idiomatic rather than textually identical. A consumer has the
        same responsibilities and wire behavior in both languages, while each client
        follows its platform&apos;s dependency-injection and asynchronous programming style.
      </p>
      <LanguageTabs csharp={sameModel.csharp} java={sameModel.java} />

      <h2 id="community">A community-driven foundation for the basics</h2>
      <p>
        The motivation behind MyServiceBus is to explore a more community-driven,
        cross-platform foundation for the everyday needs of asynchronous and distributed
        applications. The project focuses first on approachable fundamentals: typed
        contracts, consumers, send and publish, request/response, retry, failure handling,
        testing, transport adapters, and observability.
      </p>
      <p>
        It is intentionally not a feature-for-feature reimplementation of MassTransit.
        The hope is that a smaller owned core can evolve through real community use and
        contributions while keeping C# and Java behavior aligned. That is a direction
        and motivation—not yet a product commitment.
      </p>

      <h2 id="transport-choice">Change transport configuration, not business intent</h2>
      <p>
        The transport is selected at the application boundary. A team can run RabbitMQ
        locally because it is lightweight and easy to host, then configure Azure Service
        Bus when deploying the same application to Azure. Contracts, consumers, and the
        code that sends, publishes, requests, and responds do not need to be rewritten.
      </p>
      <div className="flow-line" aria-label="Transport choice by environment">
        <span>Development</span><b>→</b><span>RabbitMQ</span><b>· same application model ·</b><span>Azure Service Bus</span><b>←</b><span>Azure</span>
      </div>
      <LanguageTabs csharp={chooseTransport.csharp} java={chooseTransport.java} />
      <p>
        RabbitMQ is the verified default profile, and Azure Service Bus is available
        as a verified preview with documented limitations. Each transport package owns
        its native connection and topology projection. Broker-specific settings—such as
        RabbitMQ queue arguments or Azure topology ownership—remain on the relevant
        configurator instead of leaking into message handlers.
      </p>
      <div className="callout">
        <strong>Portable does not mean identical</strong>
        <p>Run transport-level integration tests in each target environment. Switching configuration preserves the application model, but broker topology, delivery guarantees, quotas, security, and operational behavior still differ.</p>
      </div>

      <h2 id="direct-is-better">When MassTransit or direct broker APIs are the better choice</h2>
      <ul className="check-list">
        <li>You need a mature, commercially supported .NET service bus: choose MassTransit.</li>
        <li>You need a broker feature or performance control that MyServiceBus does not expose.</li>
        <li>Your organization already has a mature, enforced messaging platform with equivalent conventions.</li>
        <li>The component is a very small bridge or infrastructure tool rather than a business service.</li>
        <li>You accept broker lock-in and want the smallest possible abstraction surface.</li>
        <li>A pre-1.0 library is not appropriate for the system&apos;s stability requirements.</li>
      </ul>
      <div className="callout">
        <strong>Use the lowest useful abstraction</strong>
        <p>Choose MyServiceBus when its common behavior removes repeated application plumbing. Drop to transport configuration—or a native client—when a broker-specific capability is the point of the design.</p>
      </div>

      <h2 id="decision">A practical decision test</h2>
      <p>
        If you need typed messaging across C# and Java, familiar MassTransit-style
        semantics, consistent failure paths, and a smaller amount of repeated broker
        plumbing, MyServiceBus may be worth evaluating and contributing to. Choose
        MassTransit when its mature .NET ecosystem and commercial support matter.
        If the problem is only one native queue with unusual broker requirements, start
        with the broker API and add abstraction only when the application-level
        conventions become real.
      </p>

      <div className="next-card">
        <div><span>Next</span><strong>Decide whether messaging fits the system</strong></div>
        <Link href="/docs/distributed-systems">Distributed systems fundamentals →</Link>
      </div>
    </article>
  );
}
