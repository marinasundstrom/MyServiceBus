import Link from 'next/link';
import LanguageTabs from '../components/LanguageTabs';

const helloWorld = {
  csharp: `public record SubmitOrder(Guid OrderId);

public class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context) =>
        Console.Out.WriteLineAsync($"Order {context.Message.OrderId}");
}`,
  java: `public record SubmitOrder(UUID orderId) { }

public class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    public CompletableFuture<Void> consume(
            ConsumeContext<SubmitOrder> context) {
        System.out.println("Order " + context.getMessage().orderId());
        return CompletableFuture.completedFuture(null);
    }
}`,
};

export default function Introduction() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Introduction</p>
      <h1>Messaging across .NET and Java, without changing the mental model.</h1>
      <p className="docs-summary">
        MyServiceBus is a lightweight asynchronous service-bus runtime with aligned
        C# and Java APIs. It provides RabbitMQ-backed messaging, an in-memory
        mediator, and familiar MassTransit-style concepts.
      </p>

      <div className="callout callout-accent">
        <strong>Current scope</strong>
        <p>
          The preview focuses on RabbitMQ, portable messaging semantics, and
          verified C# ↔ Java interoperability. It is not a drop-in replacement
          for every MassTransit feature.
        </p>
      </div>

      <h2 id="why">Why MyServiceBus?</h2>
      <div className="docs-feature-grid">
        <div><span>01</span><h3>Cross-language</h3><p>Share contracts and messaging behavior across .NET and Java services.</p></div>
        <div><span>02</span><h3>Focused</h3><p>Send, publish, consume, request, retry, and test—without a framework-wide commitment.</p></div>
        <div><span>03</span><h3>Familiar</h3><p>Use concepts that feel natural to developers who already know MassTransit.</p></div>
        <div><span>04</span><h3>Explicit</h3><p>Transport capabilities and compatibility boundaries are documented rather than implied.</p></div>
      </div>

      <h2 id="same-concept">The same concept, idiomatic code</h2>
      <p>
        The APIs do not try to look identical. They preserve the same behavior
        while following each platform’s conventions for dependency injection,
        asynchronous work, and type systems.
      </p>
      <LanguageTabs csharp={helloWorld.csharp} java={helloWorld.java} />

      <div className="next-card">
        <div><span>Next</span><strong>Build your first message flow</strong></div>
        <Link href="/docs/getting-started">Getting started →</Link>
      </div>
    </article>
  );
}
