using Aspire.Hosting.ApplicationModel;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);
var javaAgentPath = Path.Combine(builder.AppHostDirectory, "agents", "opentelemetry-javaagent.jar");
var aspireCertificatePath = Path.Combine(builder.AppHostDirectory, "agents", "aspire-localhost-cert.pem");

var rabbitUser = builder.AddParameter("rabbitmq-user", "guest", secret: true);
var rabbitPassword = builder.AddParameter("rabbitmq-password", "guest", secret: true);
var postgresUser = builder.AddParameter("postgres-user", "postgres", secret: true);
var postgresPassword = builder.AddParameter("postgres-password", "postgres", secret: true);

var rabbitmq = builder.AddRabbitMQ("messaging", rabbitUser, rabbitPassword)
    .WithManagementPlugin()
    .WithImageTag("4.1.8-management-alpine");
var postgres = builder.AddPostgres("postgres", postgresUser, postgresPassword)
    .WithImageTag("17.6-alpine");
var outbox = postgres.AddDatabase("outbox");

var monitoringService = builder.AddProject<MyServiceBus_Monitoring_Server>("monitoring-service")
    .WithHttpEndpoint(name: "http")
    .WithExternalHttpEndpoints();

builder.AddProject<MyServiceBus_Dashboard>("monitoring-dashboard")
    .WithHttpEndpoint(name: "http")
    .WithEnvironment("Dashboard__MonitoringServiceAddress", monitoringService.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(monitoringService);

var csharpTestApp = builder.AddProject<TestApp>("testapp")
    .WithReference(rabbitmq)
    .WithEnvironment("MONITORING_SERVICE_URL", monitoringService.GetEndpoint("http"))
    .WithEnvironment("OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION", "explicit_bucket_histogram")
    .WithEnvironment("RABBITMQ_HOST", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RABBITMQ_PORT", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithExternalHttpEndpoints()
    .WaitFor(monitoringService)
    .WaitFor(rabbitmq);

var massTransitTestApp = builder.AddProject<TestApp_MassTransit>("testapp-masstransit")
    .WithReference(rabbitmq)
    .WithEnvironment("OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION", "explicit_bucket_histogram")
    .WithExternalHttpEndpoints()
    .WaitFor(rabbitmq);

var javaTestApp = builder.AddExecutable(
    "testapp-java",
    "gradle",
    workingDirectory: "../..",
    ":testapp:run",
    "--no-daemon")
    .WithOtelAgent(javaAgentPath)
    .WithHttpEndpoint(name: "http", env: "HTTP_PORT")
    .WithEnvironment("OTEL_EXPORTER_OTLP_CERTIFICATE", aspireCertificatePath)
    .WithEnvironment("OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION", "explicit_bucket_histogram")
    .WithEnvironment("RABBITMQ_HOST", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RABBITMQ_PORT", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("MONITORING_SERVICE_URL", monitoringService.GetEndpoint("http"))
    .WithReference(rabbitmq)
    .WithExternalHttpEndpoints()
    .WaitFor(monitoringService)
    .WaitFor(rabbitmq);

builder.AddProject<TestApp_Outbox>("outbox-csharp")
    .WithHttpEndpoint(name: "http")
    .WithReference(rabbitmq)
    .WithReference(outbox)
    .WithEnvironment("MONITORING_SERVICE_URL", monitoringService.GetEndpoint("http"))
    .WithEnvironment("RABBITMQ_HOST", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RABBITMQ_PORT", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithExternalHttpEndpoints()
    .WaitFor(monitoringService)
    .WaitFor(rabbitmq)
    .WaitFor(outbox);

builder.AddExecutable(
        "outbox-java",
        "gradle",
        workingDirectory: "../..",
        ":testapp-outbox:run",
        "--no-daemon")
    .WithHttpEndpoint(name: "http", env: "HTTP_PORT")
    .WithEnvironment("RABBITMQ_HOST", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RABBITMQ_PORT", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("POSTGRES_HOST", postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("POSTGRES_PORT", postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("POSTGRES_DATABASE", "outbox")
    .WithEnvironment("POSTGRES_USER", postgresUser)
    .WithEnvironment("POSTGRES_PASSWORD", postgresPassword)
    .WithEnvironment("MONITORING_SERVICE_URL", monitoringService.GetEndpoint("http"))
    .WithReference(rabbitmq)
    .WithReference(outbox)
    .WithExternalHttpEndpoints()
    .WaitFor(monitoringService)
    .WaitFor(rabbitmq)
    .WaitFor(outbox);

builder.Build().Run();
