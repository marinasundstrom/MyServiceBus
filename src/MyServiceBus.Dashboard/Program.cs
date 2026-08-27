using MyServiceBus.Dashboard;
using MyServiceBus.Dashboard.Components;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient<MonitoringApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MonitoringService"] ?? "http://localhost:5310");
});

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();
app.Run();
