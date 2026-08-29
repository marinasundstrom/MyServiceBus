# Transactional Outbox Showcase

This Aspire topology runs:

- PostgreSQL 17.6 with one database shared by two logical service partitions;
- RabbitMQ 4.1.8;
- a C# MyServiceBus service; and
- a Java MyServiceBus service.

Each service exposes `POST /publish`. The request inserts an application row and captures an `OutboxShowcaseMessage` in the same PostgreSQL transaction. Its service-owned delivery worker later publishes the persisted envelope through RabbitMQ. Both language-specific consumers receive publications from both origins.

Run the topology from the repository root:

```shell
aspire run --apphost src/AspireApp_Outbox/AspireApp_Outbox.csproj
```

Use the endpoint links shown for `outbox-csharp` and `outbox-java` in the Aspire dashboard:

- `POST /publish` commits one application event plus one outbox record;
- `GET /received` lists the cross-platform messages observed by that service;
- `GET /health/outbox` reports the service partition backlog and dispatcher status; and
- `GET /health/live` reports process liveness.

Publish once through each service. Both `/received` endpoints should then contain one `csharp` and one `java` origin, while both outbox health responses should show no pending records and one dispatched record in their respective service partitions.

This is an executable MVP showcase, not the complete production-promotion suite. The delivery failure matrix remains authoritative for crash, cleanup, schema-rollout, and Consumer Outbox evidence.
