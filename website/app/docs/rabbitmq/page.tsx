import Link from 'next/link';
import LanguageTabs from '../../components/LanguageTabs';

const tuning = {
  csharp: `cfg.SetPrefetchCount(16);

cfg.ReceiveEndpoint("orders", endpoint =>
{
    endpoint.PrefetchCount(32);
    endpoint.SetQueueArgument("x-queue-type", "quorum");
});`,
  java: `configurator.setPrefetchCount(16);

configurator.receiveEndpoint("orders", endpoint -> {
    endpoint.prefetchCount(32);
    endpoint.setQueueArgument("x-queue-type", "quorum");
});`,
};

export default function RabbitMq() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">RabbitMQ transport</p>
      <h1>Durable messaging, with the failure paths made visible.</h1>
      <p className="docs-summary">RabbitMQ is the preview’s broker-backed transport profile. Connection recovery, topology recovery, error queues, fault messages, and skipped messages are built into the endpoint lifecycle.</p>

      <h2>Endpoint companions</h2>
      <div className="queue-map">
        <div><span>orders</span><p>Primary delivery queue</p></div>
        <div><span>orders_error</span><p>Original messages after terminal failure</p></div>
        <div><span>orders_fault</span><p>Published <code>Fault&lt;T&gt;</code> details</p></div>
        <div><span>orders_skipped</span><p>Messages with no recognized consumer</p></div>
      </div>

      <h2>Recovery</h2>
      <p>The transport caches an open connection, recreates it with backoff after a drop, and enables the RabbitMQ clients’ automatic connection and topology recovery.</p>

      <h2>Prefetch and queue arguments</h2>
      <p>Set a global prefetch limit, override it per endpoint, and pass broker-specific queue arguments when declaring a queue.</p>
      <LanguageTabs csharp={tuning.csharp} java={tuning.java} />

      <div className="callout"><strong>Evidence-backed baseline</strong><p>The current preview is tested against RabbitMQ 4.1, specifically <code>rabbitmq:4.1.8-alpine</code> in the reproducible integration suite.</p></div>

      <div className="next-card"><div><span>Next</span><strong>Test message behavior without a broker</strong></div><Link href="/docs/testing">Testing →</Link></div>
    </article>
  );
}
