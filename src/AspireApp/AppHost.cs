using Aspire.Hosting.ApplicationModel;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);
var javaAgentPath = Path.Combine(builder.AppHostDirectory, "agents", "opentelemetry-javaagent.jar");
var aspireCertificatePath = Path.Combine(builder.AppHostDirectory, "agents", "aspire-localhost-cert.pem");

var rabbitUser = builder.AddParameter("rabbitmq-user", "guest", secret: true);
var rabbitPassword = builder.AddParameter("rabbitmq-password", "guest", secret: true);

var rabbitmq = builder.AddRabbitMQ("messaging", rabbitUser, rabbitPassword)
    .WithManagementPlugin()
    .WithImageTag("4.1.8-management-alpine");

var csharpTestApp = builder.AddProject<TestApp>("testapp")
    .WithReference(rabbitmq)
    .WithEnvironment("OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION", "explicit_bucket_histogram")
    .WithEnvironment("RABBITMQ_HOST", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RABBITMQ_PORT", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithExternalHttpEndpoints()
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
    .WithReference(rabbitmq)
    .WithExternalHttpEndpoints()
    .WaitFor(rabbitmq);

builder.Build().Run();
