import Link from 'next/link';
import LanguageTabs from '../../components/LanguageTabs';

const send = {
  csharp: `ISendEndpoint endpoint = await provider.GetSendEndpoint(
    new Uri("queue:submit-order"));

await endpoint.Send(new SubmitOrder(Guid.NewGuid()));`,
  java: `SendEndpoint endpoint = provider.getSendEndpoint(
    "queue:submit-order");

endpoint.send(new SubmitOrder(UUID.randomUUID())).join();`,
};

const publish = {
  csharp: `await publishEndpoint.Publish(
    new OrderSubmitted(orderId));`,
  java: `publishEndpoint.publish(
    new OrderSubmitted(orderId)).join();`,
};

const consume = {
  csharp: `public class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context) =>
        context.Publish(new OrderSubmitted(
            context.Message.OrderId));
}`,
  java: `public class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    public CompletableFuture<Void> consume(
            ConsumeContext<SubmitOrder> context) {
        return context.publish(new OrderSubmitted(
            context.getMessage().orderId()));
    }
}`,
};

const request = {
  csharp: `IRequestClient<SubmitOrder> client =
    serviceProvider.GetRequiredService<IRequestClient<SubmitOrder>>();

Response<OrderSubmitted> response =
    await client.GetResponseAsync<OrderSubmitted>(message);`,
  java: `RequestClient<SubmitOrder> client =
    factory.create(SubmitOrder.class);

OrderSubmitted response = client
    .getResponse(message, OrderSubmitted.class)
    .join();`,
};

export default function Concepts() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Core concepts</p>
      <h1>Model the interaction, then let the transport map it.</h1>
      <p className="docs-summary">
        MyServiceBus keeps the application model stable across C#, Java, RabbitMQ,
        and Azure Service Bus. Start with contracts, intent, endpoints, and failure
        behavior—not broker entities.
      </p>

      <h2 id="contracts">Messages are contracts</h2>
      <p>
        A message is a durable statement exchanged across an application boundary.
        Give it a business name, keep its schema independent of storage models, and
        evolve it with producers and consumers in mind. The same contract identity
        and portable envelope allow C# and Java services to understand one another.
      </p>
      <div className="callout callout-accent">
        <strong>Describe intent or fact</strong>
        <p><code>SubmitOrder</code> asks for work. <code>OrderSubmitted</code> records an outcome. Names such as <code>OrderMessage</code> hide the interaction the system is meant to perform.</p>
      </div>

      <h2 id="intent">Choose the messaging intent</h2>
      <div className="concept-comparison">
        <section><span className="tag">EVENT</span><h2>Publish</h2><p>Announce that something happened. Every subscribed receive endpoint can process its own delivery.</p><strong>Use for:</strong><p>Domain events, notifications, and integration events.</p></section>
        <section><span className="tag">COMMAND</span><h2>Send</h2><p>Deliver work to one logical endpoint. One competing consumer instance processes the delivery.</p><strong>Use for:</strong><p>Directed tasks and point-to-point operations.</p></section>
      </div>

      <h2 id="publish-or-send">Publish or send?</h2>
      <p>
        Choose based on ownership and intent, not on how many consumers happen to exist
        today. Send when the producer knows which capability owns the work. Publish when
        the producer is announcing a completed fact and should not know who reacts to it.
      </p>
      <div className="docs-feature-grid">
        <div><span>SEND</span><h3>Submit an order</h3><p>Checkout sends <code>SubmitOrder</code> to the Orders endpoint because Orders owns that operation.</p></div>
        <div><span>PUBLISH</span><h3>Announce submission</h3><p>Orders publishes <code>OrderSubmitted</code> so Billing, Fulfilment, and Analytics may react independently.</p></div>
        <div><span>SEND</span><h3>Charge a payment</h3><p>A workflow sends <code>ChargePayment</code> to the Payments endpoint and expects that capability to perform the command.</p></div>
        <div><span>PUBLISH</span><h3>Announce payment</h3><p>Payments publishes <code>PaymentCaptured</code>; it does not decide which downstream services need the fact.</p></div>
      </div>
      <div className="callout">
        <strong>A useful test</strong>
        <p>If removing every current subscriber would make the message meaningless, it is probably a command disguised as an event. If adding a new subscriber would require changing the producer, the producer is probably too coupled to its consumers.</p>
      </div>

      <h2 id="send">Send to a logical endpoint</h2>
      <p>
        Resolve <code>queue:submit-order</code> through the active transport, then send
        the command. Application code names the destination by its role; the transport
        produces the externally meaningful broker address.
      </p>
      <LanguageTabs csharp={send.csharp} java={send.java} />

      <h2 id="publish">Publish by message contract</h2>
      <p>
        Publication does not target a queue. MyServiceBus resolves the message contract
        to the transport&apos;s publish entity and delivers it to every subscribed endpoint.
        Producers do not need to know which services are listening.
      </p>
      <LanguageTabs csharp={publish.csharp} java={publish.java} />

      <h2 id="mapping">Logical model, native topology</h2>
      <p>
        MyServiceBus preserves one application concept while each transport projects
        it into native entities. These mappings belong to the transport profile and
        remain visible when you need to operate or tune the broker.
      </p>
      <div className="concept-comparison">
        <section><span className="tag">RABBITMQ</span><h2>Exchange + queue</h2><p>A published contract maps to an exchange. Bindings route deliveries to queues; a logical send targets a queue through directed delivery.</p><strong>Successful settlement:</strong><p>The delivery is acknowledged.</p></section>
        <section><span className="tag">AZURE SERVICE BUS</span><h2>Topic + queue</h2><p>A published contract maps to a topic. A subscription forwards deliveries to a receive queue; a logical send targets that queue directly.</p><strong>Successful settlement:</strong><p>The peek-locked delivery is completed.</p></section>
      </div>
      <div className="callout">
        <strong>Transport-neutral does not mean transport-blind</strong>
        <p>The application model stays portable, but ordering, quotas, topology, settlement, security, and throughput remain properties of the selected transport.</p>
      </div>

      <h2 id="consume">Consume through a receive endpoint</h2>
      <p>
        A receive endpoint is the stable application boundary where messages enter a
        consumer pipeline. Each delivery receives a context containing the message,
        headers, identifiers, addresses, cancellation, and helpers for follow-up work.
        Consumer scopes and filters are created consistently while the transport owns
        delivery and settlement.
      </p>
      <LanguageTabs csharp={consume.csharp} java={consume.java} />
      <div className="flow-line" aria-label="Message lifecycle">
        <span>Delivery</span><b>→</b><span>Receive endpoint</span><b>→</b><span>Consumer scope</span><b>→</b><span>Settle or fault</span>
      </div>
      <p>
        Successful asynchronous completion settles the delivery using the transport&apos;s
        native mechanism. An unhandled exception enters the configured retry pipeline;
        terminal failure produces fault information and preserves the original message
        according to the transport profile.
      </p>

      <h2 id="request">Request and response</h2>
      <p>
        Use a request client when the sender truly needs one typed response. MyServiceBus
        creates or resolves a response endpoint, assigns a request identifier, propagates
        correlation metadata, and matches the response or <code>Fault&lt;T&gt;</code>. The
        transport supplies the temporary address and native delivery path.
      </p>
      <LanguageTabs csharp={request.csharp} java={request.java} />
      <div className="callout">
        <strong>A timeout is an uncertain result</strong>
        <p>The default request deadline is 30 seconds. If it expires, the remote operation may still complete; make business operations idempotent and queryable when that matters.</p>
      </div>

      <h2 id="envelope">Envelope and context</h2>
      <p>
        MyServiceBus wraps each message in a portable envelope containing identifiers,
        addresses, message type URNs, headers, and host information. C# and Java use
        the same wire shape while exposing that metadata idiomatically at runtime.
      </p>

      <h2 id="failure-destinations">Failure destinations</h2>
      <p>
        Broker transports project the same failure concepts into their native topology.
        The exact movement or publication mechanism differs, but endpoint companion
        names stay recognizable across supported profiles.
      </p>
      <div className="queue-map">
        <div><span>&lt;endpoint&gt;_error</span><p>The original message after terminal processing failure.</p></div>
        <div><span>&lt;endpoint&gt;_fault</span><p>A published <code>Fault&lt;T&gt;</code> describing the failure.</p></div>
        <div><span>&lt;endpoint&gt;_skipped</span><p>A preserved delivery that has no recognized consumer.</p></div>
        <div><span>Retry pipeline</span><p>Transient failures are retried before terminal failure handling begins.</p></div>
      </div>

      <h2 id="architecture">Where architecture guidance belongs</h2>
      <p>
        Messaging primitives do not decide service boundaries, consistency requirements,
        idempotency, ordering, or recovery policy for you. Those decisions shape the
        distributed application around the bus and deserve a guide of their own.
      </p>

      <div className="next-card"><div><span>Next</span><strong>Design the system around partial failure</strong></div><Link href="/docs/distributed-systems">Distributed systems fundamentals →</Link></div>
    </article>
  );
}
