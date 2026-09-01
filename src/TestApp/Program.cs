using System;
using System.Security;
using MyServiceBus;
using TestApp;
using System.Linq;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MyServiceBus.Choreography;
using MyServiceBus.Monitoring;
using MyServiceBus.Generated;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddScoped<GeneratedConsumerAudit>();

builder.Services.AddServiceBus(x =>
{
    x.AddGeneratedConsumers();
    x.AddChoreography(new ChoreographyBuilder("sample-order-submission", "1", "TestApp.CSharp")
        .Step<SubmitOrder>("csharp-submit-order", step => step
            .OwnedBy<SubmitOrderConsumer>()
            .Publishes<OrderSubmitted>())
        .Step<OrderSubmitted>("csharp-order-submitted", step => step
            .OwnedBy<OrderSubmittedConsumer>()
            .Terminates())
        .Build());
    x.AddJobConsumer<DemoTrackedJobConsumer, DemoTrackedJob>(options => options
        .SetJobTypeName("sample-report")
        .SetConcurrentJobLimit(2)
        .SetRetry(retry => retry.Interval(1, TimeSpan.FromSeconds(1))));

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        var rabbitMqPort = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var configuredPort)
            ? configuredPort
            : 5672;

        cfg.Host(rabbitMqHost, rabbitMqPort, h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("submit-order_fault", e =>
        {
            e.ConfigureConsumer<SubmitOrderFaultConsumer>(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddServiceBusMonitoring(options =>
{
    options.ServiceAddress = new Uri(
        Environment.GetEnvironmentVariable("MONITORING_SERVICE_URL") ?? "http://localhost:5310");
    options.ApplicationName = "TestApp.CSharp";
    options.InstanceId = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
    options.Labels["group"] = "sample-system";
    options.Labels["environment"] = builder.Environment.EnvironmentName;
    options.Labels["role"] = "api";
});

builder.Services.AddHealthChecks()
    .AddMyServiceBus();

//builder.Services.AddHostedService<HostedService>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

var recurringJobs = app.Services.GetRequiredService<IRecurringJobScheduler>();
var sampleReportIdentity = new RecurringJobIdentity("sample-report", "aspire-demo");
await recurringJobs.AddOrUpdate(
    new RecurringJobDefinition(
        sampleReportIdentity,
        new FixedIntervalRecurringJobCadence(TimeSpan.FromMinutes(5)),
        "Creates a small tracked report job so recurring definitions and their executions can be observed together."),
    new DemoTrackedJob("recurring-sample", false, false));
await recurringJobs.TriggerNow(sampleReportIdentity);

var logger = app.Logger;
logger.LogInformation("🚀 Starting TestApp");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

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
    await publishEndpoint.Publish(new OrderSubmitted(Guid.NewGuid(), "replica-1"), cancellationToken);
})
.WithName("Test_Publish")
.WithTags("Test");
*/

app.MapGet("/publish", async (IMessageBus messageBus, ILogger<Program> logger, CancellationToken cancellationToken = default) =>
{
    var message = new SubmitOrder() { OrderId = Guid.NewGuid(), Message = DemoScenario.CreateSubmitMessage("csharp", shouldFault: false) };
    try
    {
        await messageBus.Publish(message, null, cancellationToken);
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

app.MapGet("/publish/fault", async (IMessageBus messageBus, ILogger<Program> logger, CancellationToken cancellationToken = default) =>
{
    var message = new SubmitOrder() { OrderId = Guid.NewGuid(), Message = DemoScenario.CreateSubmitMessage("csharp", shouldFault: true) };
    try
    {
        await messageBus.Publish(message, null, cancellationToken);
        logger.LogInformation("📤 Published fault SubmitOrder {OrderId} ✅", message.OrderId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Failed to publish fault SubmitOrder {OrderId}", message.OrderId);
        throw;
    }
})
.WithName("Test_PublishFault")
.WithTags("Test");

app.MapGet("/send", async (ISendEndpointProvider sendEndpointProvider, ILogger<Program> logger, CancellationToken cancellationToken = default) =>
{
    var sendEndpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("rabbitmq://localhost/submit-order"));
    var message = new SubmitOrder { OrderId = Guid.NewGuid(), Message = DemoScenario.CreateSubmitMessage("csharp", shouldFault: false) };
    try
    {
        await sendEndpoint.Send(message, null, cancellationToken);
        logger.LogInformation("📤 Sent SubmitOrder {OrderId} ✅", message.OrderId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Failed to send SubmitOrder {OrderId}", message.OrderId);
        throw;
    }
})
.WithName("Test_Send")
.WithTags("Test");

app.MapGet("/send/fault", async (ISendEndpointProvider sendEndpointProvider, ILogger<Program> logger, CancellationToken cancellationToken = default) =>
{
    var sendEndpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("rabbitmq://localhost/submit-order"));
    var message = new SubmitOrder { OrderId = Guid.NewGuid(), Message = DemoScenario.CreateSubmitMessage("csharp", shouldFault: true) };
    try
    {
        await sendEndpoint.Send(message, null, cancellationToken);
        logger.LogInformation("📤 Sent fault SubmitOrder {OrderId} ✅", message.OrderId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Failed to send fault SubmitOrder {OrderId}", message.OrderId);
        throw;
    }
})
.WithName("Test_SendFault")
.WithTags("Test");

app.MapPost("/schedule", async (int? delaySeconds, IMessageScheduler scheduler, CancellationToken cancellationToken) =>
{
    var delay = TimeSpan.FromSeconds(Math.Clamp(delaySeconds ?? 120, 5, 3_600));
    var message = new SubmitOrder
    {
        OrderId = Guid.NewGuid(),
        Message = DemoScenario.CreateSubmitMessage("csharp-scheduled", shouldFault: false)
    };
    var handle = await scheduler.SchedulePublish(message, delay, cancellationToken);
    return Results.Accepted($"/schedule/{handle.TokenId}", new
    {
        handle.TokenId,
        DueAtUtc = new DateTimeOffset(handle.ScheduledTime.ToUniversalTime()),
        MessageType = nameof(SubmitOrder)
    });
})
.WithName("Schedule_SubmitOrder")
.WithTags("Scheduling");

app.MapDelete("/schedule/{tokenId:guid}", async (Guid tokenId, IMessageScheduler scheduler, CancellationToken cancellationToken) =>
{
    var result = await scheduler.CancelScheduledPublish(tokenId, cancellationToken);
    return Results.Ok(new { TokenId = tokenId, Status = result.ToString() });
})
.WithName("Cancel_ScheduledSubmitOrder")
.WithTags("Scheduling");

app.MapPost("/jobs", async (
    int? delaySeconds,
    bool? failFirstAttempt,
    bool? failAlways,
    IJobClient jobs,
    CancellationToken cancellationToken) =>
{
    var job = new DemoTrackedJob(
        $"report-{DateTimeOffset.UtcNow:HHmmss}",
        failFirstAttempt ?? false,
        failAlways ?? false);
    var delay = Math.Clamp(delaySeconds ?? 0, 0, 3_600);
    var receipt = delay == 0
        ? await jobs.Submit(job, cancellationToken: cancellationToken)
        : await jobs.Schedule(DateTimeOffset.UtcNow.AddSeconds(delay), job, cancellationToken: cancellationToken);
    return Results.Accepted($"/jobs/{receipt.JobId}", receipt);
})
.WithName("Submit_DemoTrackedJob")
.WithTags("Jobs");

app.MapGet("/request", async Task<Results<Ok<string>, InternalServerError<string>>> (IRequestClient<TestRequest> client, ILogger<Program> logger, CancellationToken cancellationToken = default) =>
{
    try
    {
        var message = new TestRequest() { Message = DemoScenario.CreateRequestMessage("csharp", shouldFault: false) };
        var response = await client.GetResponseAsync<TestResponse>(message, null, cancellationToken);
        logger.LogInformation("📨 Received response {Response} ✅", response.Message.Message);
        return TypedResults.Ok(response.Message.Message);
    }
    catch (RequestFaultException requestFaultException)
    {
        logger.LogWarning(requestFaultException, "⚠️ Fault: {Message}", requestFaultException.Message);
        return TypedResults.InternalServerError(requestFaultException.Message);
    }
})
.WithName("Test_Request")
.WithTags("Test");

app.MapGet("/request/fault", async Task<Results<Ok<string>, InternalServerError<string>>> (IRequestClient<TestRequest> client, ILogger<Program> logger, CancellationToken cancellationToken = default) =>
{
    try
    {
        var message = new TestRequest() { Message = DemoScenario.CreateRequestMessage("csharp", shouldFault: true) };
        var response = await client.GetResponseAsync<TestResponse>(message, null, cancellationToken);
        logger.LogInformation("📨 Received response {Response} ✅", response.Message.Message);
        return TypedResults.Ok(response.Message.Message);
    }
    catch (RequestFaultException requestFaultException)
    {
        logger.LogWarning(requestFaultException, "⚠️ Fault: {Message}", requestFaultException.Message);
        return TypedResults.InternalServerError(requestFaultException.Message);
    }
})
.WithName("Test_RequestFault")
.WithTags("Test");


app.MapGet("/request_multi", async Task<Results<Ok<string>, InternalServerError<string>, NoContent>> (IRequestClient<TestRequest> client, ILogger<Program> logger, CancellationToken cancellationToken = default) =>
{
    var message = new TestRequest() { Message = DemoScenario.CreateRequestMessage("csharp", shouldFault: false) };
    var response = await client.GetResponseAsync<TestResponse, Fault<TestRequest>>(message, null, cancellationToken);

    if (response.Is(out Response<TestResponse>? status))
    {
        logger.LogInformation("📨 Received response {Response} ✅", status.Message.Message);
        return TypedResults.Ok(status.Message.Message);
    }
    else if (response.Is(out Response<Fault<TestRequest>>? fault))
    {
        logger.LogError("❌ Fault received: {Message}", fault.Message.Exceptions[0].Message);
        return TypedResults.InternalServerError(fault.Message.Exceptions[0].Message);
    }

    logger.LogWarning("⚠️ No content");
    return TypedResults.NoContent();
})
.WithName("Test_RequestMulti")
.WithTags("Test");

app.MapGet("/request_multi/fault", async Task<Results<Ok<string>, InternalServerError<string>, NoContent>> (IRequestClient<TestRequest> client, ILogger<Program> logger, CancellationToken cancellationToken = default) =>
{
    var message = new TestRequest() { Message = DemoScenario.CreateRequestMessage("csharp", shouldFault: true) };
    var response = await client.GetResponseAsync<TestResponse, Fault<TestRequest>>(message, null, cancellationToken);

    if (response.Is(out Response<TestResponse>? status))
    {
        logger.LogInformation("📨 Received response {Response} ✅", status.Message.Message);
        return TypedResults.Ok(status.Message.Message);
    }
    else if (response.Is(out Response<Fault<TestRequest>>? fault))
    {
        logger.LogError("❌ Fault received: {Message}", fault.Message.Exceptions[0].Message);
        return TypedResults.InternalServerError(fault.Message.Exceptions[0].Message);
    }

    logger.LogWarning("⚠️ No content");
    return TypedResults.NoContent();
})
.WithName("Test_RequestMultiFault")
.WithTags("Test");

app.MapDefaultEndpoints();

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
