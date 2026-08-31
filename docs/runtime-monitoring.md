# Runtime Monitoring

MyServiceBus Monitoring is an optional, experimental addon that builds a runtime view of one distributed application from its participating services and sub-applications. Those participants export current bus metadata, heartbeats, and bounded batches of observations to one central service. The standalone dashboard queries that service; it never connects to the participants directly.

```text
C# and Java applications
  -> general-purpose bus hooks
  -> optional monitoring exporters
  -> monitoring service (read model, optional PostgreSQL history, and HTTP API)
  -> standalone Blazor dashboard
```

Messaging does not depend on this path. If the monitoring service is unavailable or an export fails, message processing continues.

## MVP Status

The proof of concept is suitable to ship as an **experimental MVP**, not as a production monitoring system. The end-to-end Aspire stack has been exercised with C# and Java clients, RabbitMQ, the collector, HTTP queries, the WebSocket invalidation stream, and the Blazor dashboard.

The MVP includes:

- automatic application and instance registration from bus metadata
- first-class receive-endpoint summaries with replica availability, consumers, message types, addresses, transport, and current activity
- instance heartbeats and online/offline leases
- bounded client queues and interval/count-based observation batches
- cumulative and time-window sent, published, consumed, faulted, and retry metrics
- five-minute real-time throughput series with five-second buckets
- automatic replica grouping by application name and optional resource labels
- per-replica throughput, load share, p95 consume duration, retries, and failures
- observed cross-application message-flow reconstruction from correlation identifiers
- an interactive D3 message-flow map with live topology updates and throughput-weighted paths
- expandable failed-message metadata with endpoint, retry, exception, correlation, and trace details
- recent observations with optional W3C trace and span identifiers
- batch deduplication and reported dropped-observation counts
- HTTP ingest and query APIs
- WebSocket change invalidations
- a directed, responsive Blazor dashboard with persisted light, dark, and system themes
- application Overview, Metrics, and Flow drill-downs that progressively reveal messaging detail
- equivalent exporter behavior for C# and Java
- first-class outbox dispatcher operations for embedded and standalone workers, including latest backlog, oldest-undispatched age, windowed throughput, failures, lost leases, and cycle latency
- transparent history freshness, coverage, and durability status
- an optional Entity Framework Core PostgreSQL history provider with automatic migrations, deduplicated batches, bounded retention, and active-window restoration after restart

The MVP does not yet include authentication, long-range historical query views, alerting or scaling recommendations, broker queue depth, host saturation, broker administration, or payload-byte limits. PostgreSQL adds durable collection and restart recovery, but the current metric queries and dashboard still expose only the active 15-minute read-model window. The dashboard uses WebSocket invalidations to re-query HTTP snapshots, with a 15-second polling fallback.

## Dashboard Experience

The dashboard is both an operations tool and a development tool for understanding and debugging a distributed application. Its main objects are the distributed application and the services or sub-applications that participate in it—not bus instances. It is not primarily a bus-administration console. The monitoring service keeps precise MyServiceBus terms and identities, while the dashboard progressively reveals endpoints, buses, transports, and topology when they explain participant behavior.

The intended discovery path is:

1. **System** — is the distributed application healthy, active, and sufficiently observed?
2. **Service or sub-application** — what role is this participant playing, and what is happening to it?
3. **Messaging behavior** — what is it handling, sending, retrying, or failing?
4. **Topology and bus detail** — which endpoints, connections, and paths explain that behavior?
5. **Broker infrastructure** — what broker state contributes to it, once a separately configured provider exists?
6. **Controls** — what guarded operational action is appropriate, once an explicit control plane exists?

The landing page is deliberately an overview rather than a wall of diagnostics. It answers five questions at a glance:

- how many applications and replicas are participating
- how quickly messages are being handled now
- whether recent failures or retries need attention
- whether monitoring coverage is complete
- where to go for the next level of detail

The overview uses compact, reusable widgets for summary, throughput, current concerns, and drill-down links. Each widget has a stable internal identity so a future layout configuration can arrange known widgets without coupling configuration to Razor component names. User-defined JSON layouts are intentionally not exposed yet; the default information hierarchy needs to mature first.

Each application has its own progressive drill-down:

- **Overview** shows replicas, receive endpoints, current rate, latency, failures, busiest receiving paths, and secondary messaging-connection context.
- **Metrics** expands that application's throughput graph and ranks its receive endpoints without presenting endpoint activity as consumer-level metrics.
- **Flow** limits the D3 map to the application and its directly observed neighbors, while preserving exact paths in a table.

System-wide focused views remain available when the operator needs to compare applications or inspect a domain:

- **Applications** explains logical applications and replicas, then compares load, latency, retries, failures, runtime, and transport.
- **Receive endpoints** combines exported topology with current activity so configured, offline, healthy, and faulting endpoints remain distinguishable.
- **Throughput** expands the compact landing-page chart into a five-minute streamed graph and application rate breakdown.
- **Message flow** maps applications and observed message paths, with throughput encoded in line weight and exact rates available alongside the map.
- **Failures** exposes bounded failure and retry metadata without capturing message bodies or arbitrary headers.
- **Outbox** separates dispatcher backlog and delivery pressure from broker transit and consumer processing.

Graphs and maps are a continuing dashboard theme. They are implemented as distinct components rather than being embedded into individual pages, which keeps compact overview variants and full drill-down variants consistent. New domains such as sagas should follow the same shape: add only a concise health signal to the overview when it is broadly actionable, and put state distribution, transitions, faults, and correlations in a focused view.

Broker inspection is a separate source of information from MyServiceBus monitoring. A future dashboard provider may query RabbitMQ, Azure Service Bus, or another broker management API with separately configured credentials. Queue-management, purge, replay, reset, and similar commands are a further control-plane capability: they require explicit authorization, confirmation, audit, and recoverability boundaries and must not be implied by read-only monitoring access.

This supplied dashboard is one opinionated consumer of the monitoring APIs. A future engineering-focused dashboard could organize the same data around buses, endpoints, topology, and broker objects, closer to an infrastructure console, without changing this dashboard's application-developer purpose.

Scheduled work is another application-focused view. It lists bounded operational metadata for scheduled messages: application, provider, safe message type, due time, destination or intent, status, attempt, and last failure category. The status vocabulary preserves provider truth while normalizing common states such as `Pending`, `Leased` or running, `Dispatched` or completed, `Cancelled`, and `Dead` or failed. Message bodies, arbitrary headers, serialized callbacks, and application payloads remain excluded. Recurring jobs and job-consumer conveniences remain separate future concepts rather than variants of a one-time scheduled message.

The monitoring service must not query every scheduler database. Message-aware scheduling providers and job providers should export bounded snapshots or lifecycle observations from their owning application, as outbox monitoring does. The dashboard may eventually offer cancellation or retry, but those are control-plane commands and are not authorized by read-only schedule visibility.

The unprioritized [Monitoring and Control Backlog](development/monitoring-and-control-backlog.md) records the related recurring-job, alerting, broker-provider, control-plane, and future prioritization decisions.

The dashboard is usable in both dark and light environments. The selector persists an explicit light or dark preference locally across enhanced page navigation; system mode follows the operating-system preference. The reconnect dialog uses the same theme tokens, so connection status remains legible while the server is unavailable. On narrow screens, navigation becomes horizontally scrollable, cards collapse to one column, graphs and maps fit the viewport, and wide diagnostic tables scroll inside their panels instead of widening the page.

The flow map projects the same five-minute observed-flow window as the detailed path list. Applications are nodes, directional paths are links, link width reflects relative traffic, and each link reports its observed messages per second. WebSocket invalidations update the existing D3 graph in place so node positions and the operator's zoom context remain stable while rates and health change.

![Monitoring dashboard in dark theme with fictional application and host names](images/runtime-monitoring-dashboard-dark.jpg)

![Monitoring dashboard in light theme with fictional application and host names](images/runtime-monitoring-dashboard-light.jpg)

The screenshots use generated monitoring records for fictional `Commerce` applications and instances. They contain no workstation, user, or production-system identity.

## Run the Complete Stack

From the repository root:

```bash
dotnet run --project src/AspireApp --launch-profile http
```

Open the Aspire dashboard URL printed by the command, then open the `monitoring-dashboard` resource. The AppHost starts RabbitMQ, the monitoring service, the Blazor dashboard, and the C# and Java sample applications. Both sample applications self-register after their buses start.

Use the sample applications' `/publish`, `/send`, and `/request` routes to create activity. The `/request/fault` route exercises fault handling. Export intervals make dashboard updates asynchronous; allow a few seconds for a batch and UI refresh.

## Enable the C# Exporter

Install the optional exporter package, then register the addon after the bus:

```bash
dotnet add package Sundstrom.MyServiceBus.Monitoring --version 0.1.0-preview.7
```

```csharp
builder.Services.AddServiceBus(configurator =>
{
    configurator.AddConsumer<SubmitOrderConsumer>();
    configurator.UsingRabbitMq((context, rabbit) =>
        rabbit.ConfigureEndpoints(context));
});

builder.Services.AddServiceBusMonitoring(options =>
{
    options.ServiceAddress = new Uri("http://monitoring-service:8080");
    options.ApplicationName = "Orders.Api";
    options.InstanceId = Environment.MachineName;
    options.Labels["group"] = "commerce";
    options.Labels["environment"] = "production";
});
```

The exporter is registered as both an `IBusHook` and a hosted background service. The hook only enqueues immutable events; the hosted worker performs HTTP export.

## Enable the Java Exporter

Reference `myservicebus-monitoring`, add monitoring before building the service provider, and start the exporter after the bus:

```groovy
implementation 'io.github.marinasundstrom.myservicebus:myservicebus-monitoring:0.1.0-preview.7'
```

```java
MonitoringExporterOptions monitoring = new MonitoringExporterOptions();
monitoring.setServiceAddress(URI.create("http://monitoring-service:8080"));
monitoring.setApplicationName("Orders.Worker");
monitoring.getLabels().put("group", "commerce");
monitoring.getLabels().put("environment", "production");

MonitoringExporter exporter = MonitoringServices.addMonitoring(services, monitoring);
ServiceProvider provider = services.buildServiceProvider();
MessageBus bus = provider.getRequiredService(MessageBus.class);

bus.start();
exporter.start(provider.getRequiredService(BusInspectionProvider.class));
```

Close the exporter during graceful application shutdown so it can make its bounded final flush.

## Identity, Replicas, and Labels

`ApplicationName` is the logical identity of a participating service or sub-application within the distributed application. All running processes with the same name are shown as replicas of that participant. `InstanceId` identifies an individual replica, and `BusId` distinguishes a bus within that process. A bus address is displayed as transport context but is not used as the unique runtime identity.

Labels are optional bounded resource metadata. The dashboard recognizes `group` as a simple display grouping and also shows labels such as `environment` and `role`. Labels do not replace identity: the view is effectively `group → application → replicas`. Labels common to every replica are projected onto the application summary; conflicting instance labels remain visible only on those instances.

## General-Purpose Hooks

Hooks are a core extension seam, not a monitoring-specific API. Applications and other addons may implement `IBusHook` in C# or `BusHook` in Java to observe immutable lifecycle and message-operation events. Hook exceptions are isolated from message outcomes. A hook should return quickly and should not perform remote I/O on the message pipeline.

The monitoring exporter is one implementation of this seam. Its bounded local queue, batching, heartbeats, and collector protocol remain in the optional monitoring package.

Outbox dispatcher monitoring is implemented for C# and Java delivery services. Each polling cycle contributes one bounded observation for its logical service partition and worker owner. The collector combines the latest backlog snapshot with windowed lease, dispatch, failure, and lost-lease counts, and the dashboard presents those signals in **Dispatcher operations**. Embedded delivery and standalone worker fleets use the same model, so a slow or undersized dispatcher remains visible as a delivery bottleneck.

The monitoring service never connects directly to application databases or mutates persistence state. Message bodies, arbitrary headers, persisted record identities, SQL, and connection details remain excluded. Inbox duplicate outcomes, cleanup progress, and alerting are not yet implemented. Durable monitoring history is available only through the monitoring service's optional PostgreSQL provider. See the [runtime monitoring proposal](proposals/runtime-monitoring.md#outbox-and-inbox-operations) for the longer-term boundary.

## Service API

The prototype uses `/api/monitoring/v1` for both ingest and query operations.

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/metadata` | Register or replace an instance's bus metadata |
| `POST` | `/observations:batch` | Submit one sequenced observation batch |
| `POST` | `/heartbeat` | Renew an existing instance lease |
| `GET` | `/history` | Query storage durability, history availability, freshness, gaps, and retained-window coverage |
| `GET` | `/applications` | Query application aggregates |
| `GET` | `/instances?application=...` | Query application instances |
| `GET` | `/endpoints?application=...&windowSeconds=60` | Query receive-endpoint topology, availability, and windowed activity |
| `GET` | `/metadata/{application}/{instanceId}/{busId}` | Query current bus metadata |
| `GET` | `/observations?application=...&limit=100` | Query recent observations |
| `GET` | `/metrics?application=...&windowSeconds=60&byInstance=true` | Query bounded-window rates, counts, and consume latency |
| `GET` | `/metrics/timeseries?windowSeconds=300&bucketSeconds=5` | Query bucketed rates for real-time graphs |
| `GET` | `/flow?application=...&windowSeconds=300` | Query observed correlated application flow |
| `GET` | `/outbox?application=...&windowSeconds=60` | Query dispatcher state and windowed outbox throughput |
| WebSocket | `/stream` | Receive change invalidations |

WebSocket messages indicate that metadata or observations changed; clients should re-query the authoritative HTTP read model. They are not a durable event stream.

The active read model retains metric buckets for 15 minutes and bounds its recent observation buffer. A `Complete` flag on window summaries reports whether the exporter has declared dropped observations. The history summary also reports whether storage is volatile or durable and the oldest and latest observations available in the active window. A zero rate with incomplete or stale coverage must not be interpreted as proven inactivity.

## Monitoring History Storage

In-memory storage remains the default for local development. It is intentionally volatile and starts with no history after a monitoring-service restart.

The .NET monitoring service can instead use its built-in Entity Framework Core PostgreSQL provider:

```json
{
  "ConnectionStrings": {
    "Monitoring": "Host=postgres;Database=myservicebus_monitoring;Username=monitoring;Password=..."
  },
  "Monitoring": {
    "Storage": {
      "Provider": "PostgreSql",
      "Retention": "7.00:00:00",
      "ConnectionStringName": "Monitoring"
    }
  }
}
```

The provider applies its schema migration at startup and stores the latest metadata and heartbeat per bus identity plus deduplicated observation batches as JSONB. On restart, it rebuilds the current 15-minute query window without making restored observations appear newly ingested. The seven-day retention default bounds stored batches for future historical queries; it does not yet make seven days available through the current dashboard.

Storage belongs entirely to the monitoring service. Exporters and the dashboard do not receive database credentials, use Entity Framework, or own retention behavior.

## OpenTelemetry Boundary

MyServiceBus has OpenTelemetry tracing support independently of the monitoring addon. The C# and Java clients create producer spans for send and publish operations, create consumer spans while handling messages, and propagate W3C trace context in message headers. When an application sends or publishes from inside a consumer, that new producer span inherits the active consumer span.

For example, one distributed trace can show this causal path:

```text
HTTP request → Checkout.Api
  └─ send SubmitOrder
      └─ consume SubmitOrder → Order.Worker
          └─ publish OrderAccepted
              └─ consume OrderAccepted → Inventory.Worker
```

This answers a different question from the monitoring overview. The dashboard shows the current shape and behavior of the system—applications, replicas, rates, latency, retries, failures, and observed aggregate flow. An OpenTelemetry backend shows the path and timing of one particular operation across service and messaging boundaries, including the work that caused the next message.

When running the repository through Aspire, open the Aspire dashboard's telemetry trace view to inspect this complete request-and-message chain. The inbound HTTP span, MyServiceBus producer and consumer spans, and any other instrumented application work appear together in the same trace, provided every participating service exports the propagated context. The MyServiceBus monitoring dashboard remains the aggregate runtime overview alongside that per-operation Aspire view.

The monitoring service does not receive or store OpenTelemetry spans. MyServiceBus observations may carry trace and span identifiers already present in a messaging operation. The dashboard currently surfaces those identifiers as correlation metadata on failures; a future provider can turn them into links to an independently configured tracing backend without making that backend part of the monitoring collector.

This keeps the monitoring service focused on MyServiceBus topology and runtime state while existing OpenTelemetry collectors and backends continue to own traces, metrics, and logs.

Failed-message inspection intentionally excludes message bodies and arbitrary headers. The prototype exposes only operational metadata already present in hook observations: message identity, endpoint, retry attempt, exception detail, correlation, conversation, and trace identifiers.

## Deployment and Security Boundary

The collector and dashboard are independently deployable applications, not client-library packages. Versioned Linux images for AMD64 and ARM64 are published separately:

```text
ghcr.io/marinasundstrom/myservicebus-monitoring-collector:0.1.0-preview.7
ghcr.io/marinasundstrom/myservicebus-monitoring-dashboard:0.1.0-preview.7
```

The collector listens on port `8080`. The dashboard also listens on port `8080` and reads its collector base address from `Dashboard:MonitoringServiceAddress` (for example, `Dashboard__MonitoringServiceAddress`). The default in-memory collector is intended for local development and controlled evaluation; PostgreSQL provides restart durability but does not make the unauthenticated preview a production monitoring system. Before exposing either deployment outside a trusted network, add host-level authentication and authorization, request and payload limits, TLS, and an explicit retention policy. Do not send message bodies, arbitrary headers, credentials, or broker-management data through the monitoring protocol.

For the longer-term design and vocabulary, see the [Runtime Monitoring Proposal](proposals/runtime-monitoring.md).
