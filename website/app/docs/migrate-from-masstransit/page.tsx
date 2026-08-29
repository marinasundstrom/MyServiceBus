import Link from 'next/link';

const mappings = [
  ['Publish an event', 'IPublishEndpoint.Publish', 'IPublishEndpoint.Publish', 'Preserve publish intent and validate the selected wire profile.'],
  ['Send a command', 'ISendEndpoint.Send', 'ISendEndpoint.Send', 'Preserve the destination and endpoint-name convention.'],
  ['Consume', 'IConsumer<T>.Consume', 'IConsumer<T>.Consume', 'Port registration and middleware deliberately; matching names do not make assemblies interchangeable.'],
  ['Request/response', 'IRequestClient<T>.GetResponse', 'IRequestClient<T>.GetResponseAsync', 'Re-test response, fault, timeout, and temporary endpoint behavior.'],
  ['Transactional outbox', 'Bus/Consumer Outbox providers', 'PostgreSQL Bus Outbox MVP', 'Do not point both products at the same outbox tables.'],
  ['Scheduling', 'Transport or persistent scheduler', 'Volatile provider, PostgreSQL outbox delay, or custom message-aware provider', 'Outbox delayed intent is durable; broker-native, Quartz, recurring, and persisted-cancellation adapters remain future work.'],
];

export default function MigrateFromMassTransit() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Migration path · MassTransit</p>
      <h1>Introduce MyServiceBus at a service boundary, not as a blind package swap.</h1>
      <p className="docs-summary">
        The safest path is incremental: keep the existing MassTransit services running,
        introduce one MyServiceBus C# or Java participant through the verified common wire
        subset, and move ownership only after its contracts and operational behavior pass.
      </p>

      <div className="callout callout-accent">
        <strong>Pin the compatibility target</strong>
        <p>
          MyServiceBus interoperability evidence is pinned to MassTransit 8.5.1. API
          familiarity helps developers, but it is not source compatibility and it is not
          a promise that future commercial MassTransit releases will retain the same wire behavior.
        </p>
      </div>

      <h2>API migration map</h2>
      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead><tr><th>Intent</th><th>MassTransit</th><th>MyServiceBus C#</th><th>Migration note</th></tr></thead>
          <tbody>
            {mappings.map(([intent, massTransit, myServiceBus, note]) => (
              <tr key={intent}>
                <td><strong>{intent}</strong></td><td><code>{massTransit}</code></td>
                <td><code>{myServiceBus}</code></td><td>{note}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <h2>1. Inventory the behavior you actually use</h2>
      <p>
        Record contracts, message URNs, serialization, endpoint names, routing, headers,
        request timeouts, retry and terminal-failure policy, scheduled work, outbox ownership,
        observers, and broker-specific configuration. Mark every feature outside the{' '}
        <Link href="/docs/interoperability">verified interoperability matrix</Link> before changing a service.
      </p>

      <h2>2. Add a side-by-side participant</h2>
      <p>
        A Java service is often the lowest-risk first step because it extends the estate
        without replacing a working .NET service. Give it its own receive endpoint, publish
        and consume shared contracts through the MassTransit compatibility profile, and run
        live broker tests in both directions. The same approach works for a new C# service.
      </p>
      <div className="flow-line" aria-label="Incremental MassTransit migration">
        <span>Existing MassTransit estate</span><b>→</b><span>Shared wire subset</span><b>→</b><span>One MyServiceBus service</span><b>→</b><span>Measured expansion</span>
      </div>

      <h2>3. Move one owned boundary at a time</h2>
      <ul className="check-list">
        <li>Move the consumer and its endpoint as one deployment boundary.</li>
        <li>Keep one active owner for a queue unless competing consumption is intentional.</li>
        <li>Assert message identity, correlation, faults, retries, error/skipped preservation, and shutdown behavior.</li>
        <li>Retain MassTransit where sagas, routing slips, durable scheduling, or another unmatched feature is required.</li>
        <li>Use the broker&apos;s observable state and application outcomes as migration evidence—not API resemblance alone.</li>
      </ul>

      <h2>4. Treat outbox data as an ownership handoff</h2>
      <p>
        MyServiceBus has its own normalized C# ↔ Java PostgreSQL schema. It does not read or
        write MassTransit outbox tables. Stop the old writer, drain or deliberately account
        for its pending records, deploy the MyServiceBus schema under a distinct service
        partition, and only then transfer production writes. Never have both implementations
        assume ownership of the same persisted outbox records.
      </p>

      <h2>5. Promote only the capabilities you proved</h2>
      <p>
        The current project is pre-1.0. Use the{' '}
        <Link href="/docs/platform-parity">API and readiness matrix</Link> to separate verified
        preview behavior from MVP and experimental work. A successful first service is evidence
        for that service and transport profile—not an automatic approval for every MassTransit workload.
      </p>

      <div className="next-card">
        <div><span>Next</span><strong>Check the exact common subset</strong></div>
        <Link href="/docs/interoperability">Interoperability matrix →</Link>
      </div>
    </article>
  );
}
