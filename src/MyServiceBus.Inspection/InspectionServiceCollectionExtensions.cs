using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus.Inspection;

public static class InspectionServiceCollectionExtensions
{
    public static IServiceCollection AddServiceBusInspection(this IServiceCollection services)
    {
        services.AddSingleton<IBusInspectionProvider, BusInspectionProvider>();
        return services;
    }
}
