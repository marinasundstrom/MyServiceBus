import Link from 'next/link';
import Image from 'next/image';
import LanguageTabs from '../../components/LanguageTabs';

const exporterSetup = {
  csharp: `builder.Services.AddServiceBusMonitoring(options =>
{
    options.ServiceAddress = new Uri(
        "http://monitoring-service:8080");
    options.ApplicationName = "Orders.Api";
    options.InstanceId = Environment.MachineName;
    options.Labels["group"] = "commerce";
});`,
  java: `MonitoringExporterOptions options =
    new MonitoringExporterOptions();
options.setServiceAddress(
    URI.create("http://monitoring-service:8080"));
options.setApplicationName("Orders.Worker");
options.getLabels().put("group", "commerce");

MonitoringExporter exporter =
    MonitoringServices.addMonitoring(services, options);

bus.start();
exporter.start(inspectionProvider);`,
};

const exporterInstall = {
  csharp: `dotnet add package Sundstrom.MyServiceBus.Monitoring \\
  --version 0.1.0-preview.4`,
  java: `implementation 'io.github.marinasundstrom.myservicebus:myservicebus-monitoring:0.1.0-preview.4'`,
};

export default function RuntimeMonitoring() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Optional addon · Experimental</p>
      <h1>See the distributed bus as one runtime.</h1>
      <p className="docs-summary">
        C# and Java applications export bus metadata and bounded activity batches to
        a central monitoring service. The standalone Blazor dashboard queries that
        service, so monitored applications never host dashboards or retain history.
      </p>

      <div className="callout callout-accent">
        <strong>MVP boundary</strong>
        <p>
          The end-to-end stack is ready for local development and controlled
          evaluation. It is not yet a production observability backend: storage,
          authentication, configurable retention, broker metrics,
          and automated scaling advice remain future work.
        </p>
      </div>

      <h2>Dashboard preview</h2>
      <p>
        The standalone dashboard supports persisted light, dark, and system theme
        preferences. Its live overview keeps replica health, throughput, flow, and
        failures in one scaling-oriented view.
      </p>
      <div className="dashboard-screenshots">
        <figure>
          <Image
            src="/docs/runtime-monitoring-dashboard-dark.jpg"
            alt="MyServiceBus monitoring dashboard in dark theme with fictional Commerce applications"
            width={1280}
            height={720}
          />
          <figcaption>Dark theme</figcaption>
        </figure>
        <figure>
          <Image
            src="/docs/runtime-monitoring-dashboard-light.jpg"
            alt="MyServiceBus monitoring dashboard in light theme with fictional Commerce applications"
            width={1280}
            height={720}
          />
          <figcaption>Light theme</figcaption>
        </figure>
      </div>

      <h2>How it fits together</h2>
      <ol>
        <li>General-purpose, read-only bus hooks observe lifecycle and message operations.</li>
        <li>The optional exporter copies observations into a bounded local queue.</li>
        <li>A background worker batches metadata, activity, and heartbeats over HTTP.</li>
        <li>The monitoring service owns the distributed in-memory read model.</li>
        <li>The dashboard queries HTTP snapshots and refreshes from WebSocket invalidations.</li>
      </ol>

      <p>
        Monitoring is never part of message delivery. If the service is unavailable,
        messaging continues and the bounded exporter may report dropped observations
        after connectivity returns.
      </p>

      <h2>Enable an exporter</h2>
      <LanguageTabs csharp={exporterInstall.csharp} java={exporterInstall.java} />
      <LanguageTabs csharp={exporterSetup.csharp} java={exporterSetup.java} />

      <h2>What the MVP shows</h2>
      <ul className="check-list">
        <li>Applications with automatically grouped replicas and optional resource labels</li>
        <li>Bus addresses, endpoints, consumers, messages, and bindings</li>
        <li>Live throughput graphs, windowed rates, load share, and p95 consume latency</li>
        <li>Retries, failures, completeness, and observed cross-application flow</li>
        <li>Expandable failure metadata without capturing message bodies or arbitrary headers</li>
        <li>Recent observations and optional W3C trace correlation identifiers</li>
        <li>HTTP queries plus WebSocket change invalidations</li>
      </ul>

      <h2>Replicas and flexible grouping</h2>
      <p>
        Processes with the same application name are replicas of one logical
        application, while each instance remains available for comparison. Optional
        bounded labels add a display level above that model: for example,
        <code>group=commerce</code>, <code>environment=production</code>, or
        <code>role=worker</code>. Labels organize the view; they do not replace identity.
      </p>

      <h2>Scaling-oriented, not prescriptive</h2>
      <p>
        The prototype lets an operator compare per-replica consume rate, load share,
        p95 duration, retries, and failures. It deliberately does not recommend a
        replica count. Queue depth and host saturation require transport-specific or
        external telemetry integrations and remain separate from the portable model.
      </p>

      <h2>Follow one operation across services</h2>
      <p>
        MyServiceBus already creates OpenTelemetry producer and consumer spans in
        C# and Java and propagates W3C trace context through messages. A trace can
        connect the request entering one service to the message it sends, the worker
        that consumes it, and any downstream message produced by that consumer.
      </p>
      <pre><code>{`HTTP request → Checkout.Api
  └─ send SubmitOrder
      └─ consume SubmitOrder → Order.Worker
          └─ publish OrderAccepted
              └─ consume OrderAccepted → Inventory.Worker`}</code></pre>
      <p>
        This complements the aggregate monitoring view: the dashboard describes
        current topology, throughput, latency, retries, failures, and observed flow,
        while an OpenTelemetry backend explains the path and timing of one operation.
        In the included Aspire stack, the Aspire dashboard telemetry trace view shows
        the inbound request and the message producer and consumer spans together as
        one end-to-end path, provided each participating service exports its telemetry.
        The monitoring service does not collect or persist those spans today.
        Observations retain trace and span identifiers so a future dashboard provider
        can link to the existing tracing backend without recreating it.
      </p>

      <h2>Try the complete stack</h2>
      <p>Run the Aspire AppHost from the repository root:</p>
      <pre><code>dotnet run --project src/AspireApp --launch-profile http</code></pre>
      <p>
        Open the <code>monitoring-dashboard</code> resource, then call the sample
        applications&apos; <code>/publish</code>, <code>/send</code>, or
        <code>/request/fault</code> routes. Both clients self-register with the same
        collector and appear as separate applications.
      </p>

      <h2>Deploy the components separately</h2>
      <p>
        The exporter is a client package. The in-memory collector and Blazor
        dashboard are separate applications, published as versioned Linux container
        images for AMD64 and ARM64:
      </p>
      <pre><code>{`ghcr.io/marinasundstrom/myservicebus-monitoring-collector:0.1.0-preview.4
ghcr.io/marinasundstrom/myservicebus-monitoring-dashboard:0.1.0-preview.4`}</code></pre>
      <p>
        Both listen on port <code>8080</code>. Configure the dashboard&apos;s
        <code>MonitoringService</code> setting with the collector base address. The
        prototype has no authentication or durable history yet, so keep it inside a
        controlled network.
      </p>

      <div className="next-card">
        <div><span>Next</span><strong>Test messaging behavior in isolation</strong></div>
        <Link href="/docs/testing">Testing →</Link>
      </div>
    </article>
  );
}
