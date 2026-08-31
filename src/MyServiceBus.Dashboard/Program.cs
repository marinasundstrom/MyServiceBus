using MyServiceBus.Dashboard;
using MyServiceBus.Dashboard.Components;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddOptions<DashboardOptions>()
    .BindConfiguration(DashboardOptions.SectionName)
    .Validate(options => options.MonitoringServiceAddress.IsAbsoluteUri, "Dashboard:MonitoringServiceAddress must be an absolute URI.")
    .ValidateOnStart();
builder.Services.AddHttpClient<MonitoringApiClient>((services, client) =>
{
    client.BaseAddress = services.GetRequiredService<IOptions<DashboardOptions>>().Value.MonitoringServiceAddress;
});
builder.Services.AddScoped<MonitoringDashboardState>();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();
app.Run();
