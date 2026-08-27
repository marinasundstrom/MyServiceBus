import Link from 'next/link';
import LanguageTabs from '../../components/LanguageTabs';

const harness = {
  csharp: `var harness = new InMemoryTestHarness();
harness.RegisterHandler<SomeMessage>(_ => Task.CompletedTask);

await harness.Start();
var consumed = harness.WaitForConsumed<SomeMessage>(
    TimeSpan.FromSeconds(1));

await harness.Publish(new SomeMessage());
Assert.True(await consumed);
await harness.Stop();`,
  java: `InMemoryTestHarness harness = new InMemoryTestHarness();
harness.registerHandler(SomeMessage.class,
    ctx -> CompletableFuture.completedFuture(null));

harness.start().join();
CompletableFuture<Boolean> consumed = harness.waitForConsumed(
    SomeMessage.class, Duration.ofSeconds(1));

harness.send(new SomeMessage()).join();
assertTrue(consumed.join());
harness.stop().join();`,
};

export default function Testing() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Testing</p>
      <h1>Fast feedback without a running broker.</h1>
      <p className="docs-summary">The in-memory test harness uses the same lifecycle and observation model in both clients, so application tests can exercise messaging behavior in isolation.</p>

      <h2>A minimal harness test</h2>
      <LanguageTabs csharp={harness.csharp} java={harness.java} />

      <h2>What the harness guarantees</h2>
      <ul className="check-list">
        <li>Explicit start and stop lifecycle</li>
        <li>Handler and consumer registration before delivery begins</li>
        <li>Per-delivery dependency-injection scopes for configured consumers</li>
        <li>Deterministic consumed observations with explicit timeouts</li>
        <li>Aligned behavior across C# and Java</li>
      </ul>

      <div className="callout callout-accent"><strong>Harness vs mediator</strong><p>The test harness models a hosted transport lifecycle. The standalone in-memory mediator is immediately usable after construction and is intended for in-process messaging.</p></div>

      <h2>Integration and interoperability tests</h2>
      <p>For transport-level confidence, the project uses Testcontainers with dynamically mapped ports and a pinned RabbitMQ image. Cross-language and MassTransit scenarios run in a dedicated opt-in matrix.</p>

      <div className="next-card"><div><span>Next</span><strong>See what compatibility means</strong></div><Link href="/docs/interoperability">Interoperability →</Link></div>
    </article>
  );
}
