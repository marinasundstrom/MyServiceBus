using Aspire.Hosting.ApplicationModel;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var rabbitUser = builder.AddParameter("rabbitmq-user", "guest", secret: true);
var rabbitPassword = builder.AddParameter("rabbitmq-password", "guest", secret: true);
var rabbitmq = builder.AddRabbitMQ("messaging", rabbitUser, rabbitPassword)
    .WithManagementPlugin()
    .WithImageTag("4.1.8-management-alpine");

builder.AddProject<TestApp_NServiceBus>("nservicebus")
    .WithReference(rabbitmq)
    .WithEnvironment("RABBITMQ_MANAGEMENT_URL", rabbitmq.Resource.GetEndpoint("management"))
    .WithExternalHttpEndpoints()
    .WaitFor(rabbitmq);

builder.AddProject<TestApp_MyServiceBus_NServiceBus>("myservicebus-nservicebus-profile")
    .WithReference(rabbitmq)
    .WithExternalHttpEndpoints()
    .WaitFor(rabbitmq);

builder.Build().Run();
