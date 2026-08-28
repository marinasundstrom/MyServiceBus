import Link from 'next/link';

const rows = [
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
      <p className="docs-kicker">Capability status · Preview 4</p>
      <h1>Runtime support and language tooling are tracked separately.</h1>
      <p className="docs-summary">
        MyServiceBus aims for behavioral parity without pretending every language has
        identical compiler infrastructure. This matrix distinguishes runtime primitives
        from the tooling that discovers or generates registrations for them.
      </p>

      <div className="callout callout-accent">
        <strong>How to read the matrix</strong>
        <p>
          Available and generated capabilities ship today. Manual means the runtime
          primitive exists but no build-time automation is shipped. Planned entries
          describe direction and are not compatibility promises for the current preview.
        </p>
      </div>

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
            {rows.map(([capability, ...statuses]) => (
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
        <div><span>Next</span><strong>Use generated registration on .NET</strong></div>
        <Link href="/docs/native-aot">NativeAOT for .NET →</Link>
      </div>
    </article>
  );
}
