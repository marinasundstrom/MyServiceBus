import Link from 'next/link';

const apiRows = [
  ['Publish events', 'IPublishEndpoint.Publish', 'PublishEndpoint.publish', 'Aligned + interoperable — familiar intent and verified 8.5.1 wire subset.', 'Verified preview', 'Transport acceptance edge cases still have open production gates.'],
  ['Send commands', 'ISendEndpoint.Send', 'SendEndpoint.send', 'Aligned + interoperable — familiar directed send and endpoint addressing.', 'Verified preview', 'Address normalization and broker options may evolve before 1.0.'],
  ['Consume messages', 'IConsumer<T>.Consume', 'Consumer<T>.consume', 'Idiomatic equivalent — same responsibility and behavior; Java does not copy .NET syntax.', 'Verified preview', 'Registration overloads may simplify; the typed model is a core direction.'],
  ['Request and response', 'IRequestClient<T>.GetResponseAsync', 'RequestClient<T>.getResponse', 'Idiomatic equivalent — familiar correlation and faults; Java uses class tokens where generic types are erased.', 'Verified preview', 'Convenience overloads and multi-response ergonomics may expand.'],
  ['Bus configuration and DI', 'AddServiceBus + UsingRabbitMq / UsingAzureServiceBus', 'ServiceCollection or standalone bus factory + using…', 'Deliberate divergence — C# stays MassTransit-familiar; Java follows Java composition and lifecycle idioms.', 'Verified preview', 'Java ecosystem adapters may expand without forcing one container into the core API.'],
  ['In-process mediator', 'IMediator.Send / Publish', 'Mediator.send / publish', 'MyServiceBus-native emphasis — MediatR-style semantics on the shared handler runtime; not MassTransit mediator compatibility.', 'Verified preview', 'Handler pipelines may grow; mediator remains segregated from the bus API.'],
  ['Consumer methods and handlers', '[Consumer] + AddHandler', '@MessageConsumer + addHandler', 'MyServiceBus-native — explicit mediator-friendly handlers and generated/reflected method consumers.', 'Verified preview', 'Declaration and registration conveniences may grow.'],
  ['Transactional Bus Outbox', 'UsePostgreSql + AddPostgreSqlOutboxDelivery', 'useTransaction + PostgreSqlOutboxDelivery.create', 'Deliberate divergence — normalized MyServiceBus C# ↔ Java schema, not MassTransit table compatibility.', 'MVP preview', 'Consumer Outbox, cleanup, SQL Server, and production crash gates remain.'],
  ['Transport portfolio', 'RabbitMQ + Azure Service Bus', 'RabbitMQ + Azure Service Bus', 'Deliberate scope — one portable profile per supported broker, not MassTransit transport breadth.', 'Verified preview', 'New transports may be added only with matching C# and Java behavior and evidence.'],
  ['Schedule or delay messages', 'IMessageScheduler / SchedulePublish / ScheduleSend', 'MessageScheduler.schedulePublish / scheduleSend', 'Temporary gap — familiar intent, but not MassTransit scheduler breadth or durability today.', 'Experimental', 'Durable scheduling and persisted outbox intent are next; this API may change materially.'],
  ['Runtime monitoring', 'AddServiceBusMonitoring + PostgreSqlOutboxHealth', 'MonitoringServices.addMonitoring + PostgreSqlOutboxHealth', 'MyServiceBus-native — own collector/dashboard model plus OpenTelemetry-compatible application telemetry.', 'Experimental', 'Outbox export, persistence, security, and dashboard operations are incomplete.'],
  ['Generated registration and dispatch', 'C# source generator', 'JSR 269 annotation processor', 'MyServiceBus-native — compile-time path for lower-reflection startup and mediator dispatch.', 'Verified preview', 'Generated coverage will expand with the runtime surface.'],
];

export default function PlatformParity() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">API and capability status · Current preview</p>
      <h1>What can I use today, and what could change in the future?</h1>
      <p className="docs-summary">
        Start with the developer capability, then compare the C# and Java entry points,
        the alignment choice, the evidence behind it, and the remaining compatibility
        risk. Similar means recognizable contract and behavior—not an unversioned promise
        of source or database compatibility.
      </p>

      <div className="callout callout-accent">
        <strong>Everything is still pre-1.0</strong>
        <p>
          Verified preview means the capability exists in both clients and has focused
          automated evidence. MVP preview is usable for evaluation but has named production
          gates left. Experimental means the design or operational contract can still change
          materially. None of these labels means a long-term stable API yet.
        </p>
      </div>

      <div className="callout">
        <strong>Difference is classified, not hidden</strong>
        <p>
          <em>Aligned + interoperable</em> identifies the pinned common wire contract.{' '}
          <em>Idiomatic equivalent</em> keeps behavior while adapting the API to C# or Java.{' '}
          <em>Deliberate divergence</em> and <em>MyServiceBus-native</em> identify choices we
          intend to own. <em>Temporary gap</em> means the current difference is incomplete
          work, not a desired compatibility boundary.
        </p>
      </div>

      <div className="callout">
        <strong>This is the adopter view</strong>
        <p>
          The table is intentionally curated around decisions an application team makes.
          It does not list every registration overload, generated adapter, internal descriptor,
          or test case. The repository keeps that detailed maintainer ledger separately; a
          capability reaches this page only when its public behavior and readiness can be stated clearly.
        </p>
      </div>

      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead>
            <tr>
              <th>Developer capability</th>
              <th>C# API</th>
              <th>Java API</th>
              <th>Alignment or divergence</th>
              <th>Readiness</th>
              <th>What could change</th>
            </tr>
          </thead>
          <tbody>
            {apiRows.map(([capability, csharp, java, relationship, readiness, future]) => (
              <tr key={capability}>
                <td><strong>{capability}</strong></td>
                <td><code>{csharp}</code></td>
                <td><code>{java}</code></td>
                <td>{relationship}</td>
                <td><span className="parity-status">{readiness}</span></td>
                <td>{future}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="callout">
        <strong>MassTransit is a reference point, not a moving compatibility promise</strong>
        <p>
          Wire interoperability is tested against MassTransit 8.5.1 and only for the
          documented common subset. Current MassTransit documentation is useful background
          for familiar concepts such as{' '}
          <a href="https://masstransit.massient.com/concepts/producers">send and publish ↗</a>,{' '}
          <a href="https://masstransit.massient.com/concepts/requests">requests ↗</a>,{' '}
          <a href="https://masstransit.massient.com/concepts/outbox">the outbox ↗</a>, and{' '}
          <a href="https://masstransit.massient.com/configuration/schedulers">scheduling ↗</a>.
          MyServiceBus documents and tests its own contract because later releases may evolve independently.
        </p>
      </div>

      <p>
        For the exact wire boundary, transport versions, and exclusions, use the{' '}
        <Link href="/docs/interoperability">interoperability matrix</Link>. For the
        PostgreSQL transaction boundary and remaining promotion work, use the{' '}
        <Link href="/docs/transactional-outbox">Transactional Outbox guide</Link>.
      </p>

      <h2>Equivalent experience does not mean identical syntax</h2>
      <p>
        C# deliberately keeps the MassTransit-familiar shape where it helps existing teams.
        Java expresses the equivalent responsibility through Java naming, futures, class
        tokens, explicit lifecycle, and composition idioms. Generated registration exists
        in both ecosystems, but its compiler mechanics belong in the{' '}
        <Link href="/docs/native-aot">AOT and generation guide</Link>, not in this adoption table.
      </p>

      <div className="next-card">
        <div><span>Next</span><strong>Inspect the verified wire boundary</strong></div>
        <Link href="/docs/interoperability">Interoperability matrix →</Link>
      </div>
    </article>
  );
}
