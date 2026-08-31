using MyServiceBus.Monitoring.Server;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = description => description.GroupName == "v1";
});
builder.Services.AddOutputCache();
builder.Services.AddOptions<MonitoringStorageOptions>()
    .BindConfiguration(MonitoringStorageOptions.SectionName)
    .Validate(options => options.Retention >= TimeSpan.FromMinutes(15), "Monitoring storage retention must be at least fifteen minutes.")
    .Validate(options => string.Equals(options.Provider, "InMemory", StringComparison.OrdinalIgnoreCase)
        || string.Equals(options.Provider, "PostgreSql", StringComparison.OrdinalIgnoreCase),
        "Monitoring storage provider must be InMemory or PostgreSql.")
    .ValidateOnStart();
builder.Services.AddSingleton<MonitoringRepository>();
builder.Services.AddSingleton<MonitoringChangeFeed>();
builder.Services.AddSingleton<MonitoringIngestService>();

var storageProvider = builder.Configuration[$"{MonitoringStorageOptions.SectionName}:Provider"] ?? "InMemory";
if (string.Equals(storageProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
{
    var connectionStringName = builder.Configuration[$"{MonitoringStorageOptions.SectionName}:ConnectionStringName"] ?? "Monitoring";
    var connectionString = builder.Configuration.GetConnectionString(connectionStringName)
        ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' is required for PostgreSql monitoring storage.");
    builder.Services.AddDbContextFactory<MonitoringHistoryDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddSingleton<IMonitoringHistoryStore, PostgreSqlMonitoringHistoryStore>();
}
else
{
    builder.Services.AddSingleton<IMonitoringHistoryStore, InMemoryMonitoringHistoryStore>();
}
builder.Services.AddHostedService<MonitoringHistoryRestoreService>();

var app = builder.Build();
app.UseWebSockets();
app.UseOutputCache();
app.MapOpenApi();
app.MapMonitoringApi();
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
