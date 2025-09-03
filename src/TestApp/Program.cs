using MyServiceBus;
using TestApp;
using System.Linq;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceBus(x =>
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

//builder.Services.AddHostedService<HostedService>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

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

/*
app.MapPost("/publish", async (IPublishEndpoint publishEndpoint, CancellationToken cancellationToken = default) =>
{
    await publishEndpoint.Publish(new OrderSubmitted(), cancellationToken);
})
.WithName("Test_Publish")
.WithTags("Test");
*/

app.MapGet("/publish", async (IMessageBus messageBus, CancellationToken cancellationToken = default) =>
{
    var message = new SubmitOrder() { OrderId = Guid.NewGuid(), Message = "MT Clone C#" };
    await messageBus.Publish(message, null, cancellationToken);
})
.WithName("Test_Publish")
.WithTags("Test");

app.MapGet("/send", async (ISendEndpointProvider sendEndpointProvider, CancellationToken cancellationToken = default) =>
{
    var sendEndpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("rabbitmq://localhost/submit-order-queue"));
    await sendEndpoint.Send(new SubmitOrder { OrderId = Guid.NewGuid(), Message = "MT Clone C#" }, null, cancellationToken);
})
.WithName("Test_Send")
.WithTags("Test");

app.MapGet("/request", async Task<Results<Ok<string>, InternalServerError>> (IRequestClient<TestRequest> client, CancellationToken cancellationToken = default) =>
{
    var message = new TestRequest() { Message = "Foo" };
    var response = await client.GetResponseAsync<TestResponse>(message, null, cancellationToken);

    return TypedResults.Ok(response.Message.Message);
})
.WithName("Test_Request")
.WithTags("Test");


app.MapGet("/request_multi", async Task<Results<Ok<string>, InternalServerError<string>, NoContent>> (IRequestClient<TestRequest> client, CancellationToken cancellationToken = default) =>
{
    var message = new TestRequest() { Message = "Foo" };
    var response = await client.GetResponseAsync<TestResponse, Fault<TestRequest>>(message, null, cancellationToken);

    if (response.Is(out Response<TestResponse> status))
        return TypedResults.Ok(status.Message.Message);
    else if (response.Is(out Response<Fault<TestRequest>> fault))
        return TypedResults.InternalServerError(fault.Message.Exceptions[0].Message);

    return TypedResults.NoContent();
})
.WithName("Test_RequestMulti")
.WithTags("Test");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}


public class HostedService : IHostedService
{
    private readonly IMessageBus messageBus;

    public HostedService(IMessageBus messageBus)
    {
        this.messageBus = messageBus;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(200, cancellationToken);

            var message = new SubmitOrder() { OrderId = Guid.NewGuid() };
            await messageBus.Publish(message, null, cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Ignore invalid delay values
        }
        catch (OperationCanceledException operationCanceledException)
        {
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}