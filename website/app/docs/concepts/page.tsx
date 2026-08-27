import Link from 'next/link';
import LanguageTabs from '../../components/LanguageTabs';

const send = {
  csharp: `ISendEndpoint endpoint = await provider.GetSendEndpoint(
    new Uri("queue:submit-order"));

await endpoint.Send(new SubmitOrder(Guid.NewGuid()));`,
  java: `SendEndpoint endpoint = provider.getSendEndpoint(
    "rabbitmq://localhost/submit-order");

endpoint.send(new SubmitOrder(UUID.randomUUID())).join();`,
};

const request = {
  csharp: `IRequestClient<SubmitOrder> client =
    serviceProvider.GetRequiredService<IRequestClient<SubmitOrder>>();

Response<OrderSubmitted> response =
    await client.GetResponse<OrderSubmitted>(message);`,
  java: `RequestClient<SubmitOrder> client = factory.create(SubmitOrder.class);

OrderSubmitted response = client
    .getResponse(message, OrderSubmitted.class)
    .join();`,
};

export default function Concepts() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Core concepts</p>
      <h1>Choose the intent before the API.</h1>
      <p className="docs-summary">Most applications can be designed around four operations: publish, send, consume, and request.</p>

      <div className="concept-comparison">
        <section><span className="tag">EVENT</span><h2>Publish</h2><p>Announce that something happened. Every interested queue receives its own copy.</p><strong>Use for:</strong><p>Domain events, notifications, integration events.</p></section>
        <section><span className="tag">COMMAND</span><h2>Send</h2><p>Deliver work to a specific endpoint. One consumer instance handles the message.</p><strong>Use for:</strong><p>Directed tasks and point-to-point operations.</p></section>
      </div>

      <h2 id="send">Send to a queue</h2>
      <p>Resolve a send endpoint for a logical or RabbitMQ address, then send the command.</p>
      <LanguageTabs csharp={send.csharp} java={send.java} />

      <h2 id="consume">Consume</h2>
      <p>
        Each message delivery receives a context containing the message, headers,
        addresses, and helpers for follow-up operations. A successful asynchronous
        completion acknowledges the delivery; an exception enters retry and fault handling.
      </p>

      <div className="flow-line" aria-label="Message lifecycle">
        <span>Message</span><b>→</b><span>Receive endpoint</span><b>→</b><span>Consumer scope</span><b>→</b><span>Ack or fault</span>
      </div>

      <h2 id="request">Request and response</h2>
      <p>Use a request client when the sender expects one typed response and needs correlated fault handling.</p>
      <LanguageTabs csharp={request.csharp} java={request.java} />

      <h2 id="envelope">Envelope and context</h2>
      <p>
        MyServiceBus wraps each message in a portable envelope containing identifiers,
        addresses, message type URNs, headers, and host information. C# and Java use
        the same wire shape, while contexts expose that metadata idiomatically at runtime.
      </p>

      <div className="callout callout-accent"><strong>Unknown types are preserved</strong><p>RabbitMQ routes messages that no consumer recognizes to a companion <code>_skipped</code> queue for inspection or reprocessing.</p></div>

      <div className="next-card"><div><span>Next</span><strong>Configure durable transport behavior</strong></div><Link href="/docs/rabbitmq">RabbitMQ transport →</Link></div>
    </article>
  );
}
