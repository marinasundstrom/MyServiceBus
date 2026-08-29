using System.Collections.Concurrent;
using MyServiceBus;
using MyServiceBus.Persistence;
using MyServiceBus.Persistence.PostgreSql;
using Npgsql;
using TestApp;

const string serviceName = "outbox-showcase-csharp";

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("outbox")
    ?? throw new InvalidOperationException("Connection string 'outbox' is required.");
var dataSource = NpgsqlDataSource.Create(connectionString);

builder.Services.AddSingleton(dataSource);
builder.Services.AddServiceBus(configurator =>
{
    configurator.UseBusOutbox();
    configurator.AddConsumer<OutboxShowcaseConsumer, OutboxShowcaseMessage>(
        "outbox-showcase-csharp-consumer");
    configurator.UsingRabbitMq((context, rabbit) =>
    {
        var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        var port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var value)
            ? value
            : 5672;
        rabbit.Host(host, port, credentials =>
        {
            credentials.Username("guest");
            credentials.Password("guest");
        });
        rabbit.ConfigureEndpoints(context);
    });
});
builder.Services.AddPostgreSqlOutboxDelivery(serviceName, options =>
{
    options.OwnerId = $"csharp-{Environment.MachineName}-{Environment.ProcessId}";
    options.PollInterval = TimeSpan.FromMilliseconds(250);
});

var app = builder.Build();
await PostgreSqlSchema.EnsureCreatedAsync(dataSource);
await EnsureApplicationSchema(dataSource);

app.MapPost("/publish", async (
    IPublishEndpoint publishEndpoint,
    OutboxSession outboxSession,
    CancellationToken cancellationToken) =>
{
    var message = new OutboxShowcaseMessage
    {
        EventId = Guid.NewGuid().ToString(),
        Origin = "csharp",
        CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
    };

    await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
    await using (var command = new NpgsqlCommand(
        "INSERT INTO outbox_showcase_event (event_id, origin, created_at_utc) VALUES ($1, $2, $3)",
        connection,
        transaction))
    {
        command.Parameters.AddWithValue(message.EventId);
        command.Parameters.AddWithValue(message.Origin);
        command.Parameters.AddWithValue(DateTimeOffset.Parse(message.CreatedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    using (outboxSession.UsePostgreSql(connection, transaction, serviceName))
        await publishEndpoint.Publish(message, cancellationToken: cancellationToken);

    await transaction.CommitAsync(cancellationToken);
    return Results.Accepted(value: message);
});

app.MapGet("/received", () => OutboxShowcaseConsumer.Received.ToArray());
app.MapGet("/health/outbox", async (
    PostgreSqlOutboxHealth health,
    OutboxDeliveryService delivery,
    CancellationToken cancellationToken) => new
    {
        Delivery = delivery.Status,
        Backlog = await health.GetBacklogAsync(cancellationToken)
    });
app.MapGet("/health/live", () => Results.Ok(new { Status = "live" }));

app.Run();

static async Task EnsureApplicationSchema(NpgsqlDataSource dataSource)
{
    await using var command = dataSource.CreateCommand("""
        CREATE TABLE IF NOT EXISTS outbox_showcase_event (
            event_id text PRIMARY KEY,
            origin text NOT NULL,
            created_at_utc timestamptz NOT NULL
        );
        """);
    await command.ExecuteNonQueryAsync();
}

namespace TestApp
{
    public sealed class OutboxShowcaseMessage
    {
        public string EventId { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
    }

    public sealed class OutboxShowcaseConsumer : IConsumer<OutboxShowcaseMessage>
    {
        public static ConcurrentQueue<OutboxShowcaseMessage> Received { get; } = new();

        public Task Consume(ConsumeContext<OutboxShowcaseMessage> context)
        {
            Received.Enqueue(context.Message);
            return Task.CompletedTask;
        }
    }
}
