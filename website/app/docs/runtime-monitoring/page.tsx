import Link from 'next/link';
import LanguageTabs from '../../components/LanguageTabs';

const exporterSetup = {
  csharp: `builder.Services.AddServiceBusMonitoring(options =>
{
    options.ServiceAddress = new Uri(
        "http://monitoring-service:8080");
    options.ApplicationName = "Orders.Api";
    options.InstanceId = Environment.MachineName;
});`,
  java: `MonitoringExporterOptions options =
    new MonitoringExporterOptions();
options.setServiceAddress(
    URI.create("http://monitoring-service:8080"));
options.setApplicationName("Orders.Worker");

MonitoringExporter exporter =
    MonitoringServices.addMonitoring(services, options);

bus.start();
exporter.start(inspectionProvider);`,
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
          authentication, retention, time-window rates, retry detail, and flow
          reconstruction remain future work.
        </p>
      </div>

      <h2>How it fits together</h2>
      <ol>
        <li>General-purpose, read-only bus hooks observe lifecycle and message operations.</li>
        <li>The optional exporter copies observations into a bounded local queue.</li>
        <li>A background worker batches metadata, activity, and heartbeats over HTTP.</li>
        <li>The monitoring service owns the distributed in-memory read model.</li>
        <li>The dashboard polls the query API and displays applications and instances.</li>
      </ol>

      <p>
        Monitoring is never part of message delivery. If the service is unavailable,
        messaging continues and the bounded exporter may report dropped observations
        after connectivity returns.
      </p>

      <h2>Enable an exporter</h2>
      <LanguageTabs csharp={exporterSetup.csharp} java={exporterSetup.java} />

      <h2>What the MVP shows</h2>
      <ul className="check-list">
        <li>Applications, process instances, online leases, clients, and transports</li>
        <li>Bus addresses, endpoints, consumers, messages, and bindings</li>
        <li>Cumulative sent, published, consumed, and faulted activity</li>
        <li>Recent observations and optional W3C trace correlation identifiers</li>
        <li>HTTP queries plus WebSocket change invalidations</li>
      </ul>

      <h2>OpenTelemetry stays independent</h2>
      <p>
        The monitoring service does not collect or persist OpenTelemetry data.
        Observations retain trace and span identifiers so a future dashboard provider
        can link to an existing tracing backend without recreating one.
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

      <div className="next-card">
        <div><span>Next</span><strong>Test messaging behavior in isolation</strong></div>
        <Link href="/docs/testing">Testing →</Link>
      </div>
    </article>
  );
}
