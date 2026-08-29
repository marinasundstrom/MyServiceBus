import Link from 'next/link';

const versions = [
  ['MyServiceBus', '0.1.0-preview.6', 'Only the newest preview is actively supported before 1.0.'],
  ['.NET', '.NET 10', 'C# packages target net10.0 and use the .NET 10 BCL.'],
  ['Java', 'Java 17 or newer', 'Published bytecode and APIs target Java 17; Temurin 17 is the release-gating JDK.'],
  ['PostgreSQL', '17 / 17.6 baseline', 'Transactional outbox and inbox provider.'],
  ['RabbitMQ', '4.1 / 4.1.8 baseline', 'Declared RabbitMQ transport profile.'],
  ['Azure Service Bus', 'Standard tier', 'Live cloud topology and delivery profile.'],
  ['MassTransit peer', '8.5.1', 'Pinned interoperability evidence, not a promise for future releases.'],
  ['NServiceBus peer', '10.2.8', 'Separate RabbitMQ directed-send profile only.'],
];

export default function SupportedVersions() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Release baseline · Current preview</p>
      <h1>Supported versions are explicit release boundaries.</h1>
      <p className="docs-summary">
        MyServiceBus uses modern platform APIs without turning an untested runtime,
        broker, or interoperability peer into an accidental support promise.
      </p>

      <div className="callout callout-accent">
        <strong>Language target and runtime target are not the same decision</strong>
        <p>
          The C# packages target <code>net10.0</code>, which defines the available .NET
          framework surface. Java publishes Java 17-compatible class files and APIs while
          using modern Java 17 features and idioms. Newer JDKs are expected to run the
          packages, but they are not release-gating environments today.
        </p>
      </div>

      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead><tr><th>Component</th><th>Supported line</th><th>Scope</th></tr></thead>
          <tbody>
            {versions.map(([component, version, scope]) => (
              <tr key={component}>
                <td><strong>{component}</strong></td>
                <td><code>{version}</code></td>
                <td>{scope}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <h2>Modern, idiomatic clients within those baselines</h2>
      <p>
        C# stays familiar to modern .NET and MassTransit users. Java uses records, sealed
        types where useful, <code>Instant</code>, <code>Duration</code>,{' '}
        <code>CompletionStage</code>, annotation processing, and Java-style composition.
        Behavioral parity does not require either client to imitate the other language.
      </p>

      <div className="callout">
        <strong>Changing a baseline requires evidence</strong>
        <p>
          A new target framework, Java bytecode level, broker line, or compatibility peer
          becomes supported only after the repository baseline and its conformance gates
          are updated together.
        </p>
      </div>

      <p>
        See <Link href="/docs/platform-parity">platform parity</Link> for API readiness and{' '}
        <Link href="/docs/interoperability">interoperability</Link> for the verified wire boundary.
      </p>
    </article>
  );
}
