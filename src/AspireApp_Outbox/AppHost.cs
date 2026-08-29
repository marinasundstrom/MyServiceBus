using Projects;

var builder = DistributedApplication.CreateBuilder(args);

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

builder.AddProject<TestApp_Outbox>("outbox-csharp")
    .WithHttpEndpoint(name: "http")
    .WithReference(rabbitmq)
    .WithReference(outbox)
    .WithEnvironment("RABBITMQ_HOST", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RABBITMQ_PORT", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithExternalHttpEndpoints()
    .WaitFor(rabbitmq)
    .WaitFor(outbox);

builder.AddExecutable(
        "outbox-java",
        "gradle",
        workingDirectory: "../..",
        ":testapp-outbox:run",
        "--no-daemon")
    .WithHttpEndpoint(name: "http", env: "HTTP_PORT")
    .WithReference(rabbitmq)
    .WithReference(outbox)
    .WithEnvironment("RABBITMQ_HOST", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("RABBITMQ_PORT", rabbitmq.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("POSTGRES_HOST", postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("POSTGRES_PORT", postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("POSTGRES_DATABASE", "outbox")
    .WithEnvironment("POSTGRES_USER", postgresUser)
    .WithEnvironment("POSTGRES_PASSWORD", postgresPassword)
    .WithExternalHttpEndpoints()
    .WaitFor(rabbitmq)
    .WaitFor(outbox);

builder.Build().Run();
