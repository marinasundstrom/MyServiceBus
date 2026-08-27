import Link from 'next/link';

export default function NServiceBusInterop() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">NServiceBus interoperability</p>
      <h1>A separate profile, tested against the real peer.</h1>
      <p className="docs-summary">Use the NServiceBus JSON serializer when a MyServiceBus endpoint must exchange directed RabbitMQ messages with NServiceBus. Raw JSON remains a neutral payload format with no NServiceBus-specific behavior.</p>

      <div className="version-table" role="table" aria-label="NServiceBus verified baseline">
        <div role="row"><span role="cell">NServiceBus</span><strong role="cell">10.2.8</strong></div>
        <div role="row"><span role="cell">RabbitMQ transport</span><strong role="cell">11.2.1</strong></div>
        <div role="row"><span role="cell">RabbitMQ</span><strong role="cell">4.1.8</strong></div>
        <div role="row"><span role="cell">Topology</span><strong role="cell">Conventional, classic queues</strong></div>
      </div>

      <h2>Verified directions</h2>
      <ul className="check-list">
        <li>MyServiceBus C# → NServiceBus</li>
        <li>NServiceBus → MyServiceBus C#</li>
        <li>MyServiceBus Java → NServiceBus</li>
        <li>NServiceBus → MyServiceBus Java</li>
      </ul>

      <h2>Configure the serializer</h2>
      <p>In C#, call <code>SetSerializer&lt;NServiceBusJsonMessageSerializer&gt;()</code>. In Java, call <code>setSerializer(NServiceBusJsonMessageSerializer.class)</code>. Configure it globally for a dedicated bus or only on the receive endpoint that forms the NServiceBus boundary.</p>

      <h2>What the profile maps</h2>
      <p>Outbound messages use PascalCase JSON and carry NServiceBus message identity, enclosed contract type, intent, time sent, conversation, correlation, reply, and related-message headers. Inbound JSON is matched from the enclosed type and bound case-insensitively. Contract annotations can override the local C# or Java type name when both sides need one shared identity.</p>

      <div className="callout"><strong>Current boundary</strong><p>This is a directed-send compatibility claim. Publish/subscribe, request/response, recoverability, auditing, sagas, and the wider NServiceBus platform are not yet verified.</p></div>

      <h2>Runnable peer</h2>
      <p>The isolated <code>src/AspireApp_NServiceBus</code> stack runs a real NServiceBus service and a MyServiceBus service configured with the NServiceBus profile on their own RabbitMQ resource. Each peer exposes a send endpoint, making both directions easy to exercise without mixing this profile into the general MassTransit sample stack.</p>

      <div className="next-card"><div><span>Compatibility policy</span><strong>See every verified peer and transport boundary</strong></div><Link href="/docs/interoperability">Interoperability →</Link></div>
    </article>
  );
}
