# Runtime Monitoring

MyServiceBus Monitoring is an optional, experimental addon that builds a distributed runtime view from participating MyServiceBus applications. Applications export current bus metadata, heartbeats, and bounded batches of observations to one central service. The standalone dashboard queries that service; it never connects to the applications directly.

```text
C# and Java applications
  -> general-purpose bus hooks
  -> optional monitoring exporters
  -> monitoring service (in-memory read model and HTTP API)
  -> standalone Blazor dashboard
```

Messaging does not depend on this path. If the monitoring service is unavailable or an export fails, message processing continues.

## MVP Status

The proof of concept is suitable to ship as an **experimental MVP**, not as a production monitoring system. The end-to-end Aspire stack has been exercised with C# and Java clients, RabbitMQ, the collector, HTTP queries, the WebSocket invalidation stream, and the Blazor dashboard.

The MVP includes:

- automatic application and instance registration from bus metadata
- current endpoint, consumer, message, binding, address, and transport metadata
- instance heartbeats and online/offline leases
- bounded client queues and interval/count-based observation batches
- cumulative sent, published, consumed, and faulted counters
- recent observations with optional W3C trace and span identifiers
- batch deduplication and reported dropped-observation counts
- HTTP ingest and query APIs
- WebSocket change invalidations
- a standalone Blazor runtime overview
- equivalent exporter behavior for C# and Java

The MVP does not yet include authentication, durable storage, retention, time-window throughput rates, retry-specific observations, message-flow reconstruction, alerting, payload-byte limits, or a production deployment model. The dashboard currently refreshes the query API every two seconds; the WebSocket endpoint is available for consumers but is not yet used by the Blazor UI.

## Run the Complete Stack

From the repository root:

```bash
dotnet run --project src/AspireApp --launch-profile http
```

Open the Aspire dashboard URL printed by the command, then open the `monitoring-dashboard` resource. The AppHost starts RabbitMQ, the monitoring service, the Blazor dashboard, and the C# and Java sample applications. Both sample applications self-register after their buses start.

Use the sample applications' `/publish`, `/send`, and `/request` routes to create activity. The `/request/fault` route exercises fault handling. Export intervals make dashboard updates asynchronous; allow a few seconds for a batch and UI refresh.

## Enable the C# Exporter

Install or reference `MyServiceBus.Monitoring`, then register the addon after the bus:

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
});
```

The exporter is registered as both an `IBusHook` and a hosted background service. The hook only enqueues immutable events; the hosted worker performs HTTP export.

## Enable the Java Exporter

Reference `myservicebus-monitoring`, add monitoring before building the service provider, and start the exporter after the bus:

```java
MonitoringExporterOptions monitoring = new MonitoringExporterOptions();
monitoring.setServiceAddress(URI.create("http://monitoring-service:8080"));
monitoring.setApplicationName("Orders.Worker");

MonitoringExporter exporter = MonitoringServices.addMonitoring(services, monitoring);
ServiceProvider provider = services.buildServiceProvider();
MessageBus bus = provider.getRequiredService(MessageBus.class);

bus.start();
exporter.start(provider.getRequiredService(BusInspectionProvider.class));
```

Close the exporter during graceful application shutdown so it can make its bounded final flush.

## General-Purpose Hooks

Hooks are a core extension seam, not a monitoring-specific API. Applications and other addons may implement `IBusHook` in C# or `BusHook` in Java to observe immutable lifecycle and message-operation events. Hook exceptions are isolated from message outcomes. A hook should return quickly and should not perform remote I/O on the message pipeline.

The monitoring exporter is one implementation of this seam. Its bounded local queue, batching, heartbeats, and collector protocol remain in the optional monitoring package.

## Service API

The prototype uses `/api/monitoring/v1` for both ingest and query operations.

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/metadata` | Register or replace an instance's bus metadata |
| `POST` | `/observations:batch` | Submit one sequenced observation batch |
| `POST` | `/heartbeat` | Renew an existing instance lease |
| `GET` | `/applications` | Query application aggregates |
| `GET` | `/instances?application=...` | Query application instances |
| `GET` | `/metadata/{application}/{instanceId}/{busId}` | Query current bus metadata |
| `GET` | `/observations?application=...&limit=100` | Query recent observations |
| WebSocket | `/stream` | Receive change invalidations |

WebSocket messages indicate that metadata or observations changed; clients should re-query the authoritative HTTP read model. They are not a durable event stream.

## OpenTelemetry Boundary

The monitoring service does not receive or store OpenTelemetry data. MyServiceBus observations may carry trace and span identifiers already present in the messaging operation. A future dashboard integration can use those identifiers to link to an independently configured tracing backend.

This keeps the monitoring service focused on MyServiceBus topology and runtime state while existing OpenTelemetry collectors and backends continue to own traces, metrics, and logs.

## Deployment and Security Boundary

The current in-memory service is intended for local development and controlled evaluation. Before exposing it outside a trusted network, add host-level authentication and authorization, request and payload limits, TLS, durable persistence if history is required, and an explicit retention policy. Do not send message bodies, arbitrary headers, credentials, or broker-management data through the monitoring protocol.

For the longer-term design and vocabulary, see the [Runtime Monitoring Proposal](proposals/runtime-monitoring.md).
