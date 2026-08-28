import Link from 'next/link';
import CodeViewer from '../../components/CodeViewer';

const installGenerator = `dotnet add package Sundstrom.MyServiceBus.Generators \\
  --version 0.1.0-preview.4`;

const generatedCatalog = `using MyServiceBus.Generated;

builder.Services.AddServiceBus(configurator =>
{
    configurator.AddGeneratedConsumers();

    configurator.UsingRabbitMq((context, rabbit) =>
    {
        rabbit.ConfigureEndpoints(context);
    });
});`;

const manualCatalog = `static void AddApplicationConsumers(
    IBusRegistrationConfigurator configurator)
{
    configurator.AddConsumer<SubmitOrderConsumer, SubmitOrder>();
    configurator.AddConsumer<OrderSubmittedConsumer, OrderSubmitted>();
}`;

const javaDependencies = `dependencies {
    implementation "io.github.marinasundstrom.myservicebus:myservicebus:0.1.0-preview.4"
    annotationProcessor "io.github.marinasundstrom.myservicebus:myservicebus-processor:0.1.0-preview.4"
}`;

const javaCatalog = `GeneratedConsumerCatalog.INSTANCE.register(configurator);`;

export default function NativeAot() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">.NET and Java optimization · Work in progress</p>
      <h1>Run MyServiceBus in native applications.</h1>
      <p className="docs-summary">
        The current preview can compile an application using MyServiceBus as a .NET
        NativeAOT or GraalVM Native Image executable. Generated consumer catalogs make
        that path practical in C# and Java—and can reduce startup work even when the
        application continues to use its managed runtime.
      </p>

      <div className="callout callout-accent">
        <strong>Current support boundary</strong>
        <p>
          Native execution is an MVP proof of concept, not a blanket compatibility promise.
          The generated mediator path is tested as a native executable in both runtimes.
          Serialization, interface-message proxies, third-party libraries, and custom
          extensions can still require application-specific AOT configuration.
        </p>
      </div>

      <h2>Decide whether AOT fits the application</h2>
      <p>
        AOT is a deployment tradeoff, not an automatic performance upgrade. It is worth
        considering when startup time, cold starts, memory use, or shipping a native executable
        matter. A managed runtime can be the better choice for a long-running service where JIT
        optimization, dynamic behavior, simpler diagnostics, and mature framework integration
        matter more.
      </p>
      <p>
        Measure the complete application under a representative workload before choosing.
        AOT analyzes a closed program, so code reached through reflection may need generated
        metadata or explicit preservation. That responsibility includes the serializer,
        dependency-injection container, and other application dependencies—not only MyServiceBus.
      </p>
      <p>
        Native AOT is easiest when the application is designed for it from the beginning.
        Retrofitting a large system with many libraries can require substantial work when those
        dependencies rely on dynamic code or do not provide AOT metadata. In that situation,
        keeping the managed runtime and using generated registration may deliver the useful
        startup optimization without forcing the entire dependency graph onto the native path.
      </p>

      <div className="callout">
        <strong>Start with generated registration</strong>
        <p>
          Source generation and native compilation are separate choices. A generated catalog
          removes runtime consumer discovery and reflective registration work from startup.
          For many applications, adopting that optimization while keeping JIT compilation may
          be the right stopping point.
        </p>
      </div>

      <h2>Use generated registration in .NET</h2>
      <p>
        Add the analyzer package to the application project, import the generated
        namespace, and register the catalog once during bus configuration.
      </p>
      <CodeViewer code={installGenerator} label="Install the .NET source generator" language="shell" />
      <CodeViewer code={generatedCatalog} label="Register the generated C# consumer catalog" language="csharp" />
      <p>
        The generator discovers accessible <code>IConsumer&lt;TMessage&gt;</code> implementations
        and attributed consumer methods in the application. <Link href="/docs/consumer-methods">
        Consumer-method behavior</Link> is documented separately because it is useful in
        managed applications too.
      </p>

      <h2>Write the equivalent catalog by hand</h2>
      <p>
        Source generation improves the experience; it is not a separate runtime
        model. A hand-written catalog has the same dispatch and AOT characteristics.
      </p>
      <CodeViewer code={manualCatalog} label="Hand-written C# consumer catalog" language="csharp" />

      <h2>Use generated registration in Java</h2>
      <p>
        Add the optional JSR 269 annotation processor, then register its catalog. It does
        not require a particular application framework or classpath scanning. Applications
        can write the equivalent explicit registrations when annotation processing is not desired.
      </p>
      <CodeViewer code={javaDependencies} label="Install the Java annotation processor" language="groovy" />
      <CodeViewer code={javaCatalog} label="Configure and register the generated Java consumer catalog" language="java" />

      <h2>Keep application dependencies AOT-compatible</h2>
      <p>
        In Java, <code>ServiceCollection.createAot()</code> selects the included factory-only
        container and avoids Guice activation. An application may instead adapt its existing
        container, provided that container and its registrations support the selected native-image path.
      </p>
      <p>
        Serializer configuration remains an application choice. .NET applications may opt into
        source-generated <code>System.Text.Json</code> metadata through their serializer, and both
        clients accept explicit serializer factories. Check every custom filter, transport,
        serializer, and DI adapter used by the application rather than assuming the generated
        consumer catalog makes the entire dependency graph AOT-safe.
      </p>

      <h2>What the MVP proves</h2>
      <p>
        CI publishes and runs a .NET NativeAOT application and builds and runs a GraalVM Native
        Image application. Each uses a generated consumer method with the mediator and verifies
        message, context, cancellation, and service binding. This establishes application-level
        native execution for the supported path; it does not yet certify every transport and extension.
      </p>

      <div className="callout">
        <strong>Preparing for .NET 11 Runtime Async</strong>
        <p>
          .NET 11 introduces preview runtime-managed async with NativeAOT support. An experimental
          CI smoke now compiles a consumer with the feature enabled, suspends it at an actual{' '}
          <code>await</code>, and resumes it through generated dispatch. The same gate rebuilds
          the core abstractions and mediator runtime for .NET 11 with Runtime Async. Normal packages
          still target .NET 10; the preview target is an opt-in source-compatibility proof. A future
          stable target should compare async performance with the feature enabled and disabled.
          Runtime Async is an optimization, not a requirement for compiling today&apos;s async state
          machines with NativeAOT. See the{' '}
          <a href="https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime#runtime-async">
            official .NET 11 Runtime Async guidance
          </a>.
        </p>
      </div>

      <h2>Current measurements</h2>
      <p>
        The initial Apple M1 microbenchmarks show why AOT should be measured rather than assumed
        faster. They exclude broker I/O and are evidence for this proof of concept, not production
        capacity estimates.
      </p>
      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead><tr><th>Client and workload</th><th>Managed/JIT</th><th>Native AOT</th><th>Result</th></tr></thead>
          <tbody>
            <tr><td>C# generated method invocation</td><td>165.4M ops/s</td><td>157.4M ops/s</td><td>Native 5% lower</td></tr>
            <tr><td>Java generated mediator dispatch</td><td>136,724 ops/s</td><td>83,922 ops/s</td><td>Native 39% lower</td></tr>
          </tbody>
        </table>
      </div>

      <h3>Typed registration cost</h3>
      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead><tr><th>Client</th><th>Reflection</th><th>Explicit typed</th><th>Result</th></tr></thead>
          <tbody>
            <tr><td>.NET 10</td><td>2.282 µs, 7.21 KB</td><td>1.626 µs, 6.68 KB</td><td>29% lower time, 7% lower allocation</td></tr>
            <tr><td>Java 21</td><td>0.718 µs</td><td>0.704 µs</td><td>2% lower; confidence intervals overlap</td></tr>
          </tbody>
        </table>
      </div>

      <p>
        Current JIT execution wins the steady-state dispatch measurements. The Java native result
        predates the factory-only container. Startup time, memory, binary size, serialization,
        and broker-backed throughput still need representative measurement.
      </p>

      <div className="callout">
        <strong>Preview boundary</strong>
        <p>
          Prefer concrete message contracts on the current .NET native path. Anonymous interface
          message proxies are not yet covered. Treat broker transports, custom serialization,
          custom filters, and third-party DI integrations as application-specific validation points.
        </p>
      </div>

      <div className="next-card">
        <div><span>Next</span><strong>Compare runtime and language-tooling support</strong></div>
        <Link href="/docs/platform-parity">Platform parity →</Link>
      </div>
    </article>
  );
}
