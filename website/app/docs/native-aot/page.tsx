import Link from 'next/link';
import CodeViewer from '../../components/CodeViewer';

const installGenerator = `dotnet add package Sundstrom.MyServiceBus.Generators \\
  --version 0.1.0-preview.6`;

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

const generatedJson = `[JsonSerializable(typeof(SubmitOrder))]
internal partial class ApplicationJsonContext : JsonSerializerContext
{
}

var serialization = new EnvelopeSerializerFactory(
    ApplicationJsonContext.Default.Options);

builder.Services.AddServiceBus(configurator =>
{
    configurator.AddSerializer(serialization, isSerializer: true);
    configurator.AddDeserializer(serialization, isDefault: true);
});`;

const jsonBenchmark = `dotnet run -c Release \\
  --project benchmarks/MyServiceBus.Benchmarks -- \\
  --filter '*JsonSerializationBenchmarks*'`;

const javaDependencies = `dependencies {
    implementation "io.github.marinasundstrom.myservicebus:myservicebus:0.1.0-preview.6"
    annotationProcessor "io.github.marinasundstrom.myservicebus:myservicebus-processor:0.1.0-preview.6"
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

      <h2>Optimize within your application platform</h2>
      <p>
        This is not guidance for choosing .NET over Java or Java over .NET. MyServiceBus keeps the
        messaging model familiar across both so a team can use the platform appropriate to each
        service. The useful comparison is local to that platform: which registration, serializer,
        and runtime mode gives an existing C# or Java application the best result for the metric it
        actually values?
      </p>
      <h3>C# and .NET</h3>
      <p>
        .NET NativeAOT publishes IL as a platform-specific, self-contained executable without a
        runtime JIT. Evaluate it for startup, cold-start consistency, memory footprint, deployment
        shape, and environments where runtime code generation is unavailable. NativeAOT also
        requires trimming and whole-application analysis; dynamic assembly loading and runtime code
        generation are not available. Start with Microsoft&apos;s{' '}
        <a href="https://learn.microsoft.com/dotnet/core/deploying/native-aot/">
          Native AOT deployment overview
        </a>{' '}and{' '}
        <a href="https://learn.microsoft.com/dotnet/core/deploying/trimming/prepare-libraries-for-trimming">
          trimming guidance for libraries
        </a>.
      </p>
      <p>
        Generated consumer registration and source-generated JSON metadata can both improve the
        managed CoreCLR path; neither requires the application to publish with NativeAOT.
      </p>
      <p>
        Compare reflection-capable defaults, generated registration, and source-generated JSON on
        CoreCLR, then compare the same statically described application under NativeAOT. CoreCLR may
        lead warmed throughput while NativeAOT may lead startup or memory. The matrix keeps those
        dimensions separate instead of naming one universal winner.
      </p>

      <h3>Java and GraalVM</h3>
      <p>
        Java applications may compile the ordinary MyServiceBus API with GraalVM Native Image.
        Native Image applies closed-world reachability analysis and emits an executable instead of
        starting on HotSpot. Evaluate startup, memory, and deployment benefits against warmed JIT
        throughput. Reflection, resources, proxies, serializers, and frameworks can require explicit
        metadata. See GraalVM&apos;s official{' '}
        <a href="https://www.graalvm.org/latest/reference-manual/native-image/">
          Native Image reference
        </a>{' '}and{' '}
        <a href="https://www.graalvm.org/latest/reference-manual/native-image/metadata/">
          reachability metadata guide
        </a>.
      </p>
      <p>
        Compare reflection-based and generated registration on the JVM, application-configured
        Jackson, and the same statically described application as a Native Image executable. A
        warmed JVM and Native Image optimize for different outcomes, so startup, resident memory,
        peak throughput, allocation, image size, and build time belong in distinct columns.
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

      <h2>Provide generated JSON metadata separately</h2>
      <p>
        Consumer catalogs and JSON contracts solve different problems. The MyServiceBus generator
        owns consumer registration; the application owns its <code>System.Text.Json</code> policy.
        Pass the application context to the built-in serializer factory without exposing JSON
        metadata in the portable serializer contract.
      </p>
      <CodeViewer code={generatedJson} label="Configure source-generated JSON metadata" language="csharp" />
      <p>
        The same metadata handles application payloads on send and receive. MyServiceBus processes
        its envelope fields directly, so the context only needs application message contracts—not
        every closed envelope type. Omitting options keeps the reflection-capable managed default.
      </p>

      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead><tr><th>.NET JSON mode</th><th>Managed runtime</th><th>NativeAOT</th><th>Comparison row</th></tr></thead>
          <tbody>
            <tr><td>Default reflective metadata</td><td>Supported</td><td>Not the strict path</td><td>Envelope and Raw baseline</td></tr>
            <tr><td>Application source-generated metadata</td><td>Supported</td><td>Verified by native smoke</td><td>Envelope and Raw generated</td></tr>
          </tbody>
        </table>
      </div>

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
      <p>
        The .NET smoke also performs a source-generated JSON envelope round trip with no reflection
        resolver fallback before dispatch. That verifies the built-in envelope&apos;s application-metadata
        boundary in the published native executable.
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
        C# and Java results are shown separately because their runtimes, native compilers, harnesses,
        and historical test conditions differ. Each table compares modes within one platform; neither
        is a C#-versus-Java ranking. The measurements exclude broker I/O and are proof-of-concept
        evidence, not production capacity estimates.
      </p>

      <h3>C# and .NET</h3>
      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead><tr><th>Workload</th><th>.NET 10 CoreCLR</th><th>.NET NativeAOT</th><th>Observation</th></tr></thead>
          <tbody>
            <tr><td>Generated method invocation</td><td>165.4M ops/s</td><td>157.4M ops/s</td><td>Native measured 5% lower</td></tr>
          </tbody>
        </table>
      </div>

      <h4>Typed registration cost on .NET 10</h4>
      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead><tr><th>Mode</th><th>Time</th><th>Allocation</th><th>Observation</th></tr></thead>
          <tbody>
            <tr><td>Reflection</td><td>2.282 µs</td><td>7.21 KB</td><td>Baseline</td></tr>
            <tr><td>Explicit typed</td><td>1.626 µs</td><td>6.68 KB</td><td>29% lower time, 7% lower allocation</td></tr>
          </tbody>
        </table>
      </div>

      <h3>JSON metadata comparison</h3>
      <p>
        The committed .NET harness compares reflective and source-generated metadata independently
        for envelope/raw serialization and deserialization, with allocation diagnostics. Run the full
        benchmark before publishing numbers:
      </p>
      <CodeViewer code={jsonBenchmark} label="Run the .NET JSON metadata matrix" language="shell" />
      <p>
        Warm throughput and allocation are only two columns. Cold startup, first-message cost,
        retained memory, and NativeAOT published size need process-level measurements and will be
        recorded separately rather than inferred from warmed microbenchmarks.
      </p>

      <h3>Java and GraalVM</h3>
      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead><tr><th>Workload</th><th>GraalVM 21 JIT</th><th>GraalVM Native Image</th><th>Observation</th></tr></thead>
          <tbody>
            <tr><td>Generated mediator dispatch</td><td>136,724 ops/s</td><td>83,922 ops/s</td><td>Native measured 39% lower</td></tr>
          </tbody>
        </table>
      </div>

      <h4>Typed registration cost on Java 21</h4>
      <div className="parity-table-wrap">
        <table className="parity-table">
          <thead><tr><th>Reflection</th><th>Explicit typed</th><th>Observation</th></tr></thead>
          <tbody>
            <tr><td>0.718 µs</td><td>0.704 µs</td><td>2% lower; confidence intervals overlap</td></tr>
          </tbody>
        </table>
      </div>

      <p>
        The Java native result predates the factory-only container. Both platform paths still need
        representative startup, memory, binary-size, serialization, and broker-backed measurements.
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
