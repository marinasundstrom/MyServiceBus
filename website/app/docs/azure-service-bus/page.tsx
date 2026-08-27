import Link from 'next/link';

export default function AzureServiceBus() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Azure Service Bus transport</p>
      <h1>A real-cloud profile, advancing through explicit gates.</h1>
      <p className="docs-summary">The corresponding C# and Java adapters are experimental. They share the same addressing, topology, settlement, request, and failure model, and are tested against both the official emulator and a live Azure namespace.</p>

      <div className="callout callout-accent"><strong>Experimental preview</strong><p>Use the transport for evaluation. It is not yet part of the supported interoperability baseline.</p></div>

      <h2>Verified today</h2>
      <ul className="check-list">
        <li>C# ↔ Java send, publish, request, response, and fault flows</li>
        <li>Live Create-mode queues, topics, forwarding subscriptions, and cleanup</li>
        <li>Native temporary response queues with auto-delete</li>
        <li>Delivery-lock renewal during long-running C# and Java consumers</li>
        <li>MassTransit directed sends consumed by both MyServiceBus clients using explicit entity names</li>
        <li>Default message-topic naming and bidirectional publish with MassTransit for both clients</li>
        <li>C# and Java directed queue sends consumed by MassTransit</li>
        <li>C# and Java request clients receiving correlated MassTransit responses</li>
      </ul>

      <h2>MassTransit naming is a contract</h2>
      <p>Message topics, endpoint queues, subscriptions, companion failure entities, temporary response queues, and serialized addresses must resolve consistently across MyServiceBus C#, MyServiceBus Java, and MassTransit. Explicit overrides drive the real entities in both clients, and live tests now verify the formatter-derived default message topic in both publish directions. The remaining default endpoint names and complete MassTransit matrix are still required before support is declared.</p>

      <h2>Remaining promotion gates</h2>
      <ul>
        <li>Default endpoint, subscription, and companion naming parity with the pinned MassTransit version</li>
        <li>MassTransit requests answered by C# and Java MyServiceBus services</li>
        <li>Bidirectional MassTransit fault flows</li>
        <li>Cloud failure-copy and original-message completion behavior</li>
      </ul>

      <div className="next-card"><div><span>Compatibility</span><strong>Understand what each status label promises</strong></div><Link href="/docs/interoperability">Interoperability →</Link></div>
    </article>
  );
}
