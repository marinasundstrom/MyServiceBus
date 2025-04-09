using MyServiceBus;
using TestApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceBus(x =>
{
    //x.AddConsumer<SubmitOrderConsumer>();

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
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
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
    var message = new SubmitOrder() { OrderId = Guid.NewGuid() };
    var exchangeName = NamingConventions.GetExchangeName(message.GetType());
    await messageBus.Publish(message, exchangeName, cancellationToken);
})
.WithName("Test_Publish")
.WithTags("Test");

app.MapPost("/send", async (ISendEndpoint sendEndpoint, CancellationToken cancellationToken = default) =>
{
    await sendEndpoint.Send(new SubmitOrder { OrderId = Guid.NewGuid() }, cancellationToken);
})
.WithName("Test_Send")
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
        await Task.Delay(200);

        var message = new SubmitOrder() { OrderId = Guid.NewGuid() };
        var exchangeName = NamingConventions.GetExchangeName(message.GetType());
        await messageBus.Publish(message, exchangeName, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}