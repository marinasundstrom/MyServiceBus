using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var rabbitUser = builder.AddParameter("rabbitmq-user", "guest", secret: true);
var rabbitPassword = builder.AddParameter("rabbitmq-password", "guest", secret: true);

var rabbitmq = builder.AddRabbitMQ("messaging", rabbitUser, rabbitPassword, port: 5672)
    .WithManagementPlugin(port: 15672)
    .WithDataVolume(isReadOnly: false);

var csharpTestApp = builder.AddProject<TestApp>("testapp")
    .WithReference(rabbitmq)
    .WithEndpoint("https", endpoint =>
    {
        endpoint.Port = 7244;
        endpoint.IsExternal = true;
    })
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5112;
        endpoint.IsExternal = true;
    })
    .WaitFor(rabbitmq);

var javaTestApp = builder.AddJavaApp(
    "testapp-java",
    workingDirectory: "../Java/testapp",
    new JavaAppExecutableResourceOptions
    {
        ApplicationName = "build/libs/testapp-1.0-SNAPSHOT.jar",
        OtelAgentPath = "../../AspireApp/agents",
    })
    .WithEnvironment("OTEL_EXPORTER_OTLP_CERTIFICATE", "../../AspireApp/agents/aspire-localhost-cert.pem")
    .WithReference(rabbitmq)
    .WithExternalHttpEndpoints()
    .WaitFor(rabbitmq);

builder.Build().Run();
