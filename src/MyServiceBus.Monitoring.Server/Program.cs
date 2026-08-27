using MyServiceBus.Monitoring.Server;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<MonitoringRepository>();
builder.Services.AddSingleton<MonitoringChangeFeed>();

var app = builder.Build();
app.UseWebSockets();
app.MapMonitoringApi();
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
