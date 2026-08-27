import Link from 'next/link';
import LanguageTabs from '../../components/LanguageTabs';

const routeByIntent = {
  csharp: `// A command has one intended destination.
ISendEndpoint orders = await endpointProvider.GetSendEndpoint(
    new Uri("queue:submit-order"));
await orders.Send(new SubmitOrder(orderId));

// An event is a fact that any number of services may observe.
await publishEndpoint.Publish(new OrderSubmitted(orderId));`,
  java: `// A command has one intended destination.
SendEndpoint orders = endpointProvider.getSendEndpoint(
    "rabbitmq://localhost/submit-order");
orders.send(new SubmitOrder(orderId)).join();

// An event is a fact that any number of services may observe.
publishEndpoint.publish(new OrderSubmitted(orderId)).join();`,
};

const idempotentConsumer = {
  csharp: `public async Task Consume(ConsumeContext<ChargeCard> context)
{
    var operationId = context.Message.PaymentId;

    if (await payments.HasCompleted(operationId))
        return;

    await payments.Charge(operationId, context.Message.Amount);
    await payments.MarkCompleted(operationId);
}`,
  java: `public CompletableFuture<Void> consume(ConsumeContext<ChargeCard> context) {
    UUID operationId = context.getMessage().paymentId();

    return payments.hasCompleted(operationId).thenCompose(completed -> {
        if (completed) return CompletableFuture.completedFuture(null);

        return payments.charge(operationId, context.getMessage().amount())
            .thenCompose(ignored -> payments.markCompleted(operationId));
    });
}`,
};

const retryPolicy = {
  csharp: `x.AddConsumer<ReserveStockConsumer, ReserveStock>(consumer =>
{
    // Retry short-lived failures before the message is faulted.
    consumer.UseMessageRetry(retry =>
        retry.Interval(3, TimeSpan.FromSeconds(2)));
});`,
  java: `cfg.addConsumer(ReserveStockConsumer.class, ReserveStock.class,
    consumer -> {
        // Retry short-lived failures before the message is faulted.
        consumer.useMessageRetry(retry ->
            retry.interval(3, Duration.ofSeconds(2)));
    });`,
};

const traceHeaders = {
  csharp: `await publishEndpoint.Publish(
    new OrderSubmitted(orderId),
    context => context.Headers["tenant-id"] = tenantId);`,
  java: `publishEndpoint.publish(
    new OrderSubmitted(orderId),
    context -> context.getHeaders().put("tenant-id", tenantId)
).join();`,
};

export default function DistributedSystemsFundamentals() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Design guide</p>
      <h1>Design for partial failure, not the happy path.</h1>
      <p className="docs-summary">
        Distributed systems trade local simplicity for independent deployment,
        scale, and resilience. The fundamentals below help you decide where
        messaging helps—and what your application must still guarantee.
      </p>

      <div className="callout callout-accent">
        <strong>The central idea</strong>
        <p>
          A message can be delayed, delivered again, or processed after its sender
          has timed out. Make every important operation safe under those conditions.
        </p>
      </div>

      <h2 id="when-to-use-messaging">Know when asynchronous messaging helps</h2>
      <p>
        Choose asynchronous messaging when the sender can hand off work without an
        immediate answer, when bursts should be buffered, when several capabilities
        react independently to the same fact, or when work should survive a temporary
        outage in another service. It is especially useful at a real ownership boundary
        where the receiver must control its own pace.
      </p>
      <div className="concept-comparison">
        <section><span className="tag">GOOD FIT</span><h2>Decouple the work</h2><p>Accept an order now, reserve stock independently, and notify other services with an event.</p><strong>You gain:</strong><p>Load levelling, independent recovery, and fan-out.</p></section>
        <section><span className="tag">POOR FIT</span><h2>Keep it simple</h2><p>Validate a form, calculate a local value, or complete a change that needs an immediate result.</p><strong>You avoid:</strong><p>Eventual consistency, operational overhead, and harder debugging.</p></section>
      </div>
      <p>
        A broker is not a substitute for a well-placed service boundary. If two pieces
        of code always change together, share one transaction, and cannot be useful
        independently, a local call or modular monolith is often the better design.
      </p>

      <h2 id="boundaries">Start with boundaries and ownership</h2>
      <p>
        Split services around business capabilities and data ownership, not technical
        layers. A service should be able to make its own decisions with the data it
        owns. Crossing a boundary is a business interaction with latency and failure,
        even when the call looks like an ordinary method invocation.
      </p>
      <div className="docs-feature-grid">
        <div><span>01</span><h3>One owner per fact</h3><p>Choose one service as the authority for each piece of business data. Other services keep references or local projections.</p></div>
        <div><span>02</span><h3>Contracts over internals</h3><p>Messages describe durable business intent or outcomes. Avoid exposing database rows and implementation details.</p></div>
        <div><span>03</span><h3>Autonomy over chatty calls</h3><p>Prefer enough local data to make a decision. Long synchronous call chains multiply latency and failure modes.</p></div>
        <div><span>04</span><h3>Explicit invariants</h3><p>Write down what must be immediately consistent and what may converge later.</p></div>
      </div>

      <h2 id="intent">Make message intent obvious</h2>
      <p>
        A command asks one capability to do something. Name it in the imperative,
        such as <code>SubmitOrder</code>. An event reports a fact that has already
        happened, such as <code>OrderSubmitted</code>, and may have many subscribers.
        Queries and request/response are useful when the caller truly cannot proceed
        without an answer, but they keep the caller temporally coupled to the responder.
      </p>
      <LanguageTabs csharp={routeByIntent.csharp} java={routeByIntent.java} />

      <div className="flow-line" aria-label="Command and event flow">
        <span>Checkout</span><b>→ command →</b><span>Orders</span><b>→ event →</b><span>Billing · Fulfilment · Analytics</span>
      </div>

      <h2 id="delivery">Assume delivery can happen more than once</h2>
      <p>
        A broker and consumer can disagree about whether work completed—for example,
        when a consumer commits a database change and loses its connection before the
        acknowledgement arrives. Design consumers to be idempotent: processing the
        same operation again must produce the same durable result without repeating
        an irreversible side effect.
      </p>
      <LanguageTabs csharp={idempotentConsumer.csharp} java={idempotentConsumer.java} />
      <p>
        Use a stable business operation identifier, enforce uniqueness in durable
        storage, and make the side effect and processed marker atomic when possible.
        The in-memory check shown above explains the shape; production code needs a
        database constraint or transaction that remains correct across crashes and replicas.
      </p>

      <h2 id="consistency">Treat consistency as a workflow</h2>
      <p>
        A transaction cannot normally span a service database and a broker safely.
        If a state change and an outgoing message must either both happen or neither
        happen, use a transactional outbox or another durable hand-off. MyServiceBus
        does not currently provide a built-in transactional outbox, so this guarantee
        belongs in your application or persistence integration.
      </p>
      <div className="flow-line" aria-label="Transactional outbox flow">
        <span>Update domain state</span><b>+</b><span>Store outgoing message</span><b>→</b><span>Relay publishes</span><b>→</b><span>Consumer deduplicates</span>
      </div>
      <div className="callout">
        <strong>Eventual consistency needs a product decision</strong>
        <p>Define what users see while data is catching up, how long is acceptable, and how operators detect and repair a stuck workflow.</p>
      </div>

      <h2 id="failure">Classify failures before retrying</h2>
      <p>
        Retry transient failures such as brief network or dependency outages. Do not
        retry invalid input, missing business preconditions, or permanent authorization
        failures: retries only increase load and delay diagnosis. Keep the retry window
        bounded, use backoff for shared dependencies, and make the consumer idempotent first.
      </p>
      <LanguageTabs csharp={retryPolicy.csharp} java={retryPolicy.java} />
      <p>
        After retries are exhausted, MyServiceBus publishes a <code>Fault&lt;T&gt;</code>
        and moves the original delivery to the endpoint&apos;s <code>_error</code> queue.
        Treat these as operational signals for diagnosis or compensation; never blindly
        republish a failed message.
      </p>

      <h2 id="time">Timeouts create uncertainty, not cancellation</h2>
      <p>
        A timeout tells the caller that an answer did not arrive in time. It does not
        prove the remote work stopped—or even that it failed. MyServiceBus request
        clients have a default 30-second deadline. Give commands stable operation IDs,
        make retries safe, and provide a way to query the eventual result when the
        business operation may outlive the request.
      </p>
      <div className="concept-comparison">
        <section><span className="tag">SYNCHRONOUS</span><h2>Request</h2><p>The caller needs an answer now and can handle timeout or fault explicitly.</p><strong>Watch for:</strong><p>Availability coupling and cascading latency.</p></section>
        <section><span className="tag">ASYNCHRONOUS</span><h2>Message + status</h2><p>Accept work, return an operation ID, and let the caller observe progress.</p><strong>Watch for:</strong><p>State transitions, expiry, and user-facing progress.</p></section>
      </div>

      <h2 id="ordering">Avoid hidden ordering assumptions</h2>
      <p>
        Do not assume a global message order. Retries, multiple consumers, competing
        replicas, and independent queues can change completion order. Put the current
        entity version or sequence in the contract when order matters, partition work
        by the business key when the transport supports it, and make consumers reject
        or defer stale transitions explicitly.
      </p>

      <h2 id="observability">Carry context across the boundary</h2>
      <p>
        Logs alone cannot explain a workflow spread across services. Preserve message,
        correlation, and trace identifiers; record the endpoint and message type; and
        measure throughput, latency, retries, faults, and queue age. Use bounded labels
        for metrics—never message or correlation IDs.
      </p>
      <LanguageTabs csharp={traceHeaders.csharp} java={traceHeaders.java} />
      <p>
        MyServiceBus propagates W3C trace context and exposes runtime monitoring for
        aggregate flow and failures. Custom headers are useful for bounded business
        context such as a tenant, but must not contain secrets or unbounded payload data.
      </p>

      <h2 id="checklist">Review every message flow</h2>
      <ul className="check-list">
        <li>Who owns the data and the business decision?</li>
        <li>Is this a command, an event, or a query—and does its name make that clear?</li>
        <li>What happens if it is delayed, duplicated, or completed out of order?</li>
        <li>Which failures are transient, and where do exhausted messages go?</li>
        <li>What does a timeout mean to the caller, and how can the final result be found?</li>
        <li>Which consistency guarantees are required, and where is the durable hand-off?</li>
        <li>Can one slow dependency create a retry storm or exhaust consumer capacity?</li>
        <li>Can an operator trace, diagnose, and safely repair the workflow?</li>
        <li>Have the happy path, duplicate, timeout, retry, and poison-message paths been tested?</li>
      </ul>

      <div className="next-card">
        <div><span>Next</span><strong>Apply the fundamentals to messaging APIs</strong></div>
        <Link href="/docs/concepts">Core concepts →</Link>
      </div>
    </article>
  );
}
