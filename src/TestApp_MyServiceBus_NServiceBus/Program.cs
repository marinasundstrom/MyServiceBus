using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MyServiceBus;
using MyServiceBus.Serialization;
using TestApp;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddServiceBus(registration =>
{
    registration.SetSerializer<NServiceBusJsonMessageSerializer>();
    registration.UsingRabbitMq((_, rabbit) =>
    {
        var connectionUri = new Uri(
            builder.Configuration.GetConnectionString("messaging")
            ?? throw new InvalidOperationException("The RabbitMQ 'messaging' connection string is required."));
        rabbit.Host(connectionUri.Host, connectionUri.Port, host =>
        {
            if (!string.IsNullOrWhiteSpace(connectionUri.UserInfo))
            {
                var credentials = connectionUri.UserInfo.Split(':', 2);
                host.Username(Uri.UnescapeDataString(credentials[0]));
                host.Password(Uri.UnescapeDataString(credentials[1]));
            }
        });
        rabbit.ReceiveEndpoint("testapp-myservicebus-nservicebus", endpoint =>
        {
            endpoint.Handler<SubmitOrder>(context =>
            {
                Console.WriteLine(
                    $"Received NServiceBus SubmitOrder {context.Message.OrderId} from {context.Message.Message}");
                return Task.CompletedTask;
            });
        });
    });
});

builder.Services.AddHealthChecks().AddMyServiceBus();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});

app.MapGet("/send", async (
    ISendEndpointProvider sendEndpointProvider,
    CancellationToken cancellationToken) =>
{
    var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:testapp-nservicebus"));
    var message = new SubmitOrder
    {
        OrderId = Guid.NewGuid(),
        Message = "MyServiceBus NServiceBus profile"
    };
    await endpoint.Send(message, cancellationToken: cancellationToken);
    return Results.Ok(message);
});

await app.RunAsync();
