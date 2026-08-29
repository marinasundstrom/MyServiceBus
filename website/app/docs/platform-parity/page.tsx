import Link from 'next/link';

const apiRows = [
  ['Publish events', 'IPublishEndpoint.Publish', 'PublishEndpoint.publish', 'MassTransit-familiar intent; verified 8.5.1 wire subset', 'Verified preview', 'Transport acceptance edge cases still have open production gates.'],
  ['Send commands', 'ISendEndpoint.Send', 'SendEndpoint.send', 'MassTransit-familiar directed send and endpoint addressing', 'Verified preview', 'Address normalization and broker options may evolve before 1.0.'],
  ['Consume messages', 'IConsumer<T>.Consume', 'Consumer<T>.consume', 'Equivalent consumer, context, response, retry, fault, error, and skipped concepts', 'Verified preview', 'Registration overloads may simplify; the typed model is a core direction.'],
  ['Request and response', 'IRequestClient<T>.GetResponseAsync', 'RequestClient<T>.getResponse', 'MassTransit-familiar correlation, response, timeout, and fault semantics', 'Verified preview', 'Convenience overloads and multi-response ergonomics may expand.'],
  ['In-process mediator', 'IMediator.Send / Publish', 'Mediator.send / publish', 'MediatR-style semantics; MassTransit also offers a mediator', 'Verified preview', 'Handler pipelines may grow; mediator remains segregated from the bus API.'],
  ['Transactional Bus Outbox', 'UsePostgreSql + AddPostgreSqlOutboxDelivery', 'useTransaction + PostgreSqlOutboxDelivery.create', 'Same pattern, but a MyServiceBus C# ↔ Java schema—not MassTransit table compatibility', 'MVP preview', 'Consumer Outbox, cleanup, SQL Server, and production crash gates remain.'],
  ['Schedule or delay messages', 'IMessageScheduler / SchedulePublish / ScheduleSend', 'MessageScheduler.schedulePublish / scheduleSend', 'Familiar intent, without MassTransit scheduler breadth or durability today', 'Experimental', 'Durable scheduling and persisted outbox intent are next; this API may change materially.'],
  ['Runtime monitoring', 'AddServiceBusMonitoring + PostgreSqlOutboxHealth', 'MonitoringServices.addMonitoring + PostgreSqlOutboxHealth', 'MyServiceBus inspection plus OpenTelemetry-compatible application telemetry', 'Experimental', 'Outbox export, persistence, security, and dashboard operations are incomplete.'],
  ['Generated registration and dispatch', 'C# source generator', 'JSR 269 annotation processor', 'MyServiceBus compile-time path for lower-reflection startup and mediator dispatch', 'Verified preview', 'Generated coverage will expand with the runtime surface.'],
];

const toolingRows = [
  ['Interface consumer', 'Available', 'Generated', 'Available', 'Not needed'],
  ['Explicit consumer/message catalog', 'Available', 'Generated', 'Available', 'Generated'],
  ['Runtime interface discovery', 'Available', 'N/A', 'Registered class', 'N/A'],
  ['Filtered assembly discovery', 'Available', 'N/A', 'Not applicable', 'N/A'],
  ['Attributed method consumer', 'Reflection path', 'Available', 'Available', 'Available'],
  ['Grouped static consumer methods', 'Reflection path', 'Available', 'Available', 'Available'],
  ['Attribute endpoint override for IConsumer<T>', 'Reflection path', 'Available', 'Available', 'Available'],
  ['Message and context binding', 'Available', 'Available', 'Available', 'Available'],
  ['Method parameter service injection', 'Available', 'Typed generation', 'Available', 'Typed generation'],
  ['Async consumer-method response', 'Task<T> + ValueTask<T>', 'Available', 'Future<T> + Stage<T>', 'Available'],
  ['Generated direct method invocation', 'Typed adapter path', 'Available', 'Typed invoker path', 'JSR 269'],
  ['Named method endpoint', 'Attribute or fluent', 'Available', 'Annotation or explicit', 'Available'],
  ['Reflection-free method discovery and invocation', 'Typed path', 'Available', 'Typed path', 'Available'],
  ['Explicit serializer factory', 'Service-provider factory', 'Not needed', 'Serializer + deserializer', 'Not needed'],
  ['Factory-only AOT dependency injection', 'Typed Microsoft DI', 'Not needed', 'No Guice activation', 'Not needed'],
  ['External-container consumer activation', 'Consumer factory', 'Not needed', 'Consumer factory', 'Not needed'],
  ['Native executable smoke', 'Available', '.NET NativeAOT CI', 'No tracing metadata', 'GraalVM CI'],
  ['Runtime-managed async core and consumer in a native executable', 'Opt-in .NET 11 preview target', 'Generated dispatch verified', 'Different JVM model', 'Not applicable'],
  ['Source-generated JSON metadata', 'Application opt-in', 'Serializer-owned', 'Serializer-specific', 'Serializer-owned'],
];

export default function PlatformParity() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">API and capability status · Current preview</p>
      <h1>What can I use today, and what could change in the future?</h1>
      <p className="docs-summary">
        Start with the developer capability, then compare the C# and Java entry points,
        the relationship to MassTransit, the evidence behind it, and the remaining
        compatibility risk. Similar means recognizable contract and behavior—not an
        unversioned promise of source or database compatibility.
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

      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead>
            <tr>
              <th>Developer capability</th>
              <th>C# API</th>
              <th>Java API</th>
              <th>MassTransit relationship</th>
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

      <h2>Generator and runtime tooling parity</h2>
      <p>
        Behavioral parity does not require identical compiler infrastructure. This
        secondary matrix separates runtime primitives from the tooling that discovers
        or generates registrations for them.
      </p>

      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead>
            <tr>
              <th>Consumer capability</th>
              <th>.NET runtime</th>
              <th>C# generator</th>
              <th>Java runtime</th>
              <th>Java tooling</th>
            </tr>
          </thead>
          <tbody>
            {toolingRows.map(([capability, ...statuses]) => (
              <tr key={capability}>
                <td>{capability}</td>
                {statuses.map((status, index) => (
                  <td key={`${capability}-${index}`}><span className="parity-status">{status}</span></td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <h2>One descriptor model, different producers</h2>
      <p>
        The shared runtime direction is a consumer descriptor containing endpoint identity,
        message contract, activation, parameter binding, and invocation. Existing interface
        consumers, reflection, generated C#, hand-written catalogs, and Java annotation
        processing can all produce that descriptor
        without requiring identical language syntax.
      </p>

      <h2>Current practical choices</h2>
      <ul className="check-list">
        <li>C# can use reflection discovery, explicit typed registration, or the generated catalog.</li>
        <li>Java can use interface consumers, explicit registrations, reflection over named classes, or a generated catalog.</li>
        <li>C# and Java attributed static classes can group several message methods on one endpoint.</li>
        <li>Both clients bind message, context, cancellation, and scoped service parameters.</li>
        <li>Method-consumer classes do not require or use an <code>IConsumer</code> marker.</li>
        <li>Java intentionally has no implicit classpath scan or scan predicate.</li>
        <li>Java AOT applications can select a factory-only container; conventional Guice-backed setup remains available.</li>
        <li>Full application AOT support remains work in progress in both runtimes.</li>
      </ul>

      <div className="callout">
        <strong>External language integration</strong>
        <p>
          Raven is a separate product, not a MyServiceBus runtime or roadmap column.
          Its namespace-level functions could consume this descriptor model through an
          external integration without becoming part of MyServiceBus platform parity.
        </p>
      </div>

      <div className="next-card">
        <div><span>Next</span><strong>Inspect the verified wire boundary</strong></div>
        <Link href="/docs/interoperability">Interoperability matrix →</Link>
      </div>
    </article>
  );
}
