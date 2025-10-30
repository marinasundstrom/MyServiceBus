using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var csharpTestApp = builder.AddProject<TestApp>("testapp")
       .WithExternalHttpEndpoints();

var javaTestApp = builder.AddJavaApp(
    "testapp-java",
    workingDirectory: "../Java/testapp",
    new JavaAppExecutableResourceOptions
    {
        ApplicationName = "target/testapp-1.0-SNAPSHOT.jar",
        OtelAgentPath = "../../../../agents",
    })
    .WithExternalHttpEndpoints();

builder.Build().Run();
