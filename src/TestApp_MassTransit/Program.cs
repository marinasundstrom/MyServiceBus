using MassTransit;
using TestApp;
using System.Linq;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
    x.AddConsumer<OrderSubmittedConsumer>();
    x.AddConsumer<TestRequestConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        /*
        cfg.Message<SubmitOrder>(m =>
        {
            m.SetEntityName("TestApp.SubmitOrder");
        });

        cfg.Message<OrderSubmitted>(m =>
        {
            m.SetEntityName("TestApp.OrderSubmitted");
        });

        cfg.ReceiveEndpoint("submit-order-consumer", e =>
        {
            e.ConfigureConsumer<SubmitOrderConsumer>(context);
        }); */

        cfg.ConfigureEndpoints(context);
    });
});


// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

var logger = app.Logger;
logger.LogInformation("🚀 Starting TestApp_MassTransit");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    try
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
            new WeatherForecast
            (
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                summaries[Random.Shared.Next(summaries.Length)]
            ));
        return [.. forecast];
    }
    catch (ArgumentOutOfRangeException)
    {
        return Array.Empty<WeatherForecast>();
    }
    catch (OverflowException)
    {
        return Array.Empty<WeatherForecast>();
    }
})
.WithName("GetWeatherForecast");

app.MapGet("/publish", [Throws(typeof(Exception))] async (IPublishEndpoint publishEndpoint, ILogger<Program> logger, CancellationToken cancellationToken = default) =>
{
    var message = new SubmitOrder() { OrderId = Guid.NewGuid(), Message = "MT" };
    try
    {
        await publishEndpoint.Publish(message, cancellationToken);
        logger.LogInformation("📤 Published SubmitOrder {OrderId} ✅", message.OrderId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Failed to publish SubmitOrder {OrderId}", message.OrderId);
        throw;
    }
})
.WithName("Test_Publish")
.WithTags("Test");

app.MapGet("/send", [Throws(typeof(Exception))] async (ISendEndpointProvider sendEndpointProvider, ILogger<Program> logger, CancellationToken cancellationToken = default) =>
{
    try
    {
        var sendEndpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("rabbitmq://localhost/orders-queue"));
        var message = new SubmitOrder { OrderId = Guid.NewGuid(), Message = "MT" };
        await sendEndpoint.Send(message, cancellationToken);
        logger.LogInformation("📤 Sent SubmitOrder {OrderId} ✅", message.OrderId);
    }
    catch (UriFormatException ex)
    {
        logger.LogError(ex, "❌ Failed to send SubmitOrder");
        throw;
    }
})
.WithName("Test_Send")
.WithTags("Test");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
