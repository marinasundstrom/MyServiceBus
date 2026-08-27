import Link from 'next/link';

export default function Interoperability() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Interoperability</p>
      <h1>Specific promises, backed by specific tests.</h1>
      <p className="docs-summary">MyServiceBus compatibility is deliberately scoped. The current baseline verifies portable messaging across the C# client, Java client, and MassTransit through RabbitMQ.</p>

      <div className="interop-matrix" aria-label="Interoperability directions">
        <div><strong>MyServiceBus C#</strong><span>↔</span><strong>MyServiceBus Java</strong></div>
        <div><strong>MyServiceBus C#</strong><span>↔</span><strong>MassTransit 8.5.1</strong></div>
        <div><strong>MyServiceBus Java</strong><span>↔</span><strong>MassTransit 8.5.1</strong></div>
      </div>

      <h2>Verified scenarios</h2>
      <ul className="check-list">
        <li>Envelope publication and directed queue sends</li>
        <li>Correlated request, response, and fault flows</li>
        <li>Retry exhaustion and preservation in <code>_error</code></li>
        <li>Unknown-message preservation in <code>_skipped</code></li>
        <li>C# ↔ Java delivery in both directions</li>
      </ul>

      <h2>What is not implied</h2>
      <p>This is not source compatibility, full MassTransit API coverage, or a promise for every MassTransit 8.x or RabbitMQ 4.x release. Compatibility is currently tied to the documented versions and RabbitMQ transport profile.</p>

      <div className="version-table" role="table" aria-label="Supported baseline">
        <div role="row"><span role="cell">MyServiceBus</span><strong role="cell">0.1.0-preview.1</strong></div>
        <div role="row"><span role="cell">.NET</span><strong role="cell">.NET 10</strong></div>
        <div role="row"><span role="cell">Java</span><strong role="cell">17 or newer</strong></div>
        <div role="row"><span role="cell">RabbitMQ</span><strong role="cell">4.1.8 baseline</strong></div>
        <div role="row"><span role="cell">MassTransit</span><strong role="cell">8.5.1 peer</strong></div>
      </div>

      <div className="callout"><strong>Preview support</strong><p>Before 1.0, only the newest published preview is actively supported. Fixes are delivered in a newer preview.</p></div>

      <div className="next-card"><div><span>Start building</span><strong>Return to the quick start</strong></div><Link href="/docs/getting-started">Getting started →</Link></div>
    </article>
  );
}
