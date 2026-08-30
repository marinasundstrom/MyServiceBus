import Link from 'next/link';

export default function Interoperability() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Interoperability</p>
      <h1>Specific promises, backed by specific tests.</h1>
      <p className="docs-summary">MyServiceBus compatibility is deliberately scoped by peer and transport. The broad portable profile targets MassTransit, while NServiceBus support is a separate RabbitMQ directed-send profile.</p>

      <div className="interop-matrix" aria-label="Interoperability directions">
        <div><strong>MyServiceBus C#</strong><span>↔</span><strong>MyServiceBus Java</strong></div>
        <div><strong>MyServiceBus C#</strong><span>↔</span><strong>MassTransit 8.5.1</strong></div>
        <div><strong>MyServiceBus Java</strong><span>↔</span><strong>MassTransit 8.5.1</strong></div>
        <div><strong>MyServiceBus C# / Java</strong><span>↔</span><strong>NServiceBus 10.2.8</strong></div>
      </div>

      <h2>Verification is transport-specific</h2>
      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead><tr><th>Transport</th><th>Verified</th><th>Not yet verified</th></tr></thead>
          <tbody>
            <tr><td><strong>RabbitMQ</strong></td><td>Full C# / Java / MassTransit 8.5.1 matrix</td><td>Features outside the documented portable profile</td></tr>
            <tr><td><strong>Azure Service Bus</strong></td><td>Live-cloud send, publish, request, fault, naming, lock renewal, and settlement</td><td>Promotion review against every current MassTransit convention</td></tr>
            <tr><td><strong>Amazon SQS/SNS</strong></td><td>Standard-queue send and raw SNS publication in both directions for C#, Java, and MassTransit 8.5.1</td><td>FIFO queues and FIFO-specific ordering, grouping, and deduplication</td></tr>
          </tbody>
        </table>
      </div>

      <h2>What is not implied</h2>
      <p>This is not source compatibility, full MassTransit API coverage, or a promise for every MassTransit 8.x or broker release. Compatibility is tied to the documented versions and transport profiles.</p>

      <h2>Contract for every supported transport</h2>
      <p>Both MyServiceBus clients must follow the supported MassTransit peer’s addressing, entity naming, topology, native-property, and settlement conventions. A transport is not promoted from experimental to supported until C#, Java, and MassTransit can communicate in both directions through that profile’s documented conformance matrix.</p>

      <div className="callout"><strong>Azure Service Bus: verified preview</strong><p>Live Azure verifies cloud topology, default message and consumer endpoint naming, bidirectional publish, directed queue sends, correlated responses and faults, lock renewal, and terminal failure settlement between MassTransit and both MyServiceBus clients.</p></div>

      <div className="callout"><strong>Amazon SQS/SNS: standard-queue preview</strong><p>LocalStack verifies bidirectional directed sends and raw SNS-to-SQS publication between MassTransit 8.5.1 and both MyServiceBus clients. It is the default acceptance environment; the narrow real-AWS gate is reserved for emulator differences and AWS-only concerns. FIFO behavior is not part of this preview.</p></div>

      <div className="callout"><strong>NServiceBus: separate directed-send profile</strong><p>Live RabbitMQ tests verify C# and Java directed sends in both directions against real NServiceBus endpoints. Raw JSON remains neutral and does not imply NServiceBus compatibility. <Link href="/docs/nservicebus">Review the exact profile →</Link></p></div>

      <div className="version-table" role="table" aria-label="Supported baseline">
        <div role="row"><span role="cell">MyServiceBus</span><strong role="cell">0.1.0-preview.6</strong></div>
        <div role="row"><span role="cell">.NET</span><strong role="cell">.NET 10</strong></div>
        <div role="row"><span role="cell">Java</span><strong role="cell">17 or newer</strong></div>
        <div role="row"><span role="cell">RabbitMQ</span><strong role="cell">4.1.8 baseline</strong></div>
        <div role="row"><span role="cell">MassTransit</span><strong role="cell">8.5.1 peer</strong></div>
        <div role="row"><span role="cell">NServiceBus</span><strong role="cell">10.2.8 directed-send peer</strong></div>
      </div>

      <div className="callout"><strong>Preview support</strong><p>Before 1.0, only the newest published preview is actively supported. Fixes are delivered in a newer preview.</p></div>

      <p>
        The concise runtime and tooling policy is on the{' '}
        <Link href="/docs/supported-versions">supported versions page</Link>.
      </p>

      <div className="next-card"><div><span>Transport preview</span><strong>Configure Azure Service Bus and review its boundaries</strong></div><Link href="/docs/azure-service-bus">Azure Service Bus →</Link></div>
    </article>
  );
}
