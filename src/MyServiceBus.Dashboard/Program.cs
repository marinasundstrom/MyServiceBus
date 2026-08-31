using MyServiceBus.Dashboard;
using MyServiceBus.Dashboard.Components;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;

var builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient<MonitoringApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MonitoringService"] ?? "http://localhost:5310");
});
builder.Services.AddScoped<MonitoringDashboardState>();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();
app.Run();
