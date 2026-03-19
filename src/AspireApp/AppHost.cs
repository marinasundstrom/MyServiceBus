using Projects;

var builder = DistributedApplication.CreateBuilder(args);

//var rabbitmq = builder.AddRabbitMQ("messaging")
//    .WithDataVolume(isReadOnly: false);

var csharpTestApp = builder.AddProject<TestApp>("testapp")
    //.WithReference(rabbitmq)
    .WithExternalHttpEndpoints();
//.WaitFor(rabbitmq);

var javaTestApp = builder.AddJavaApp(
    "testapp-java",
    workingDirectory: "../Java/testapp",
    new JavaAppExecutableResourceOptions
    {
        ApplicationName = "build/libs/testapp-1.0-SNAPSHOT.jar",
        OtelAgentPath = "../../AspireApp/agents",
    })
    .WithEnvironment("OTEL_EXPORTER_OTLP_CERTIFICATE", "../../AspireApp/agents/aspire-localhost-cert.pem")
    //.WithReference(rabbitmq)
    .WithExternalHttpEndpoints();
//.WaitFor(rabbitmq);

builder.Build().Run();
