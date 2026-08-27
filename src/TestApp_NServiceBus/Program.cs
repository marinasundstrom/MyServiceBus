using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using NServiceBus;
using NServiceBus.Transport.RabbitMQ;
using TestApp;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var rabbitMqConnectionString = builder.Configuration.GetConnectionString("messaging")
    ?? throw new InvalidOperationException("The RabbitMQ 'messaging' connection string is required.");

var endpointConfiguration = new EndpointConfiguration("testapp-nservicebus");
var transport = endpointConfiguration.UseTransport<RabbitMQTransport>();
transport.ConnectionString(rabbitMqConnectionString);
transport.UseConventionalRoutingTopology(QueueType.Classic);

var managementUrl = builder.Configuration["RABBITMQ_MANAGEMENT_URL"];
if (!string.IsNullOrWhiteSpace(managementUrl))
{
    var connectionUri = new Uri(rabbitMqConnectionString);
    var credentials = connectionUri.UserInfo.Split(':', 2);
    transport.ManagementApiConfiguration(
        managementUrl,
        Uri.UnescapeDataString(credentials[0]),
        Uri.UnescapeDataString(credentials[1]));
}

endpointConfiguration.UseSerialization<SystemJsonSerializer>();
endpointConfiguration.SendFailedMessagesTo("testapp-nservicebus-error");
endpointConfiguration.EnableInstallers();
endpointConfiguration.Conventions().DefiningCommandsAs(
    type => type == typeof(SubmitOrder) || type == typeof(TestRequest));
endpointConfiguration.Conventions().DefiningEventsAs(type => type == typeof(OrderSubmitted));
endpointConfiguration.Conventions().DefiningMessagesAs(type => type == typeof(TestResponse));

builder.Services.AddOpenApi();
builder.Services.AddNServiceBusEndpoint(endpointConfiguration);

var app = builder.Build();
app.Logger.LogInformation("Starting TestApp_NServiceBus");

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});

app.MapGet("/send", async (IMessageSession messageSession, CancellationToken cancellationToken) =>
{
    var message = new SubmitOrder
    {
        OrderId = Guid.NewGuid(),
        Message = "NServiceBus"
    };
    var options = new SendOptions();
    options.SetDestination("testapp-myservicebus-nservicebus");
    await messageSession.Send(message, options, cancellationToken);
    return Results.Ok(message);
});

app.MapGet("/publish", async (IMessageSession messageSession, CancellationToken cancellationToken) =>
{
    var message = new OrderSubmitted(Guid.NewGuid(), "NServiceBus");
    await messageSession.Publish(message, cancellationToken);
    return Results.Ok(message);
});

await app.RunAsync();
