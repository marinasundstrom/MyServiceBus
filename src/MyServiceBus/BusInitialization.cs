using Microsoft.Extensions.DependencyInjection;

namespace MyServiceBus;

/// <summary>Initializes registered endpoints once for hosted and standalone bus startup.</summary>
public sealed class BusInitialization
{
    private readonly object sync = new();
    private bool initialized;
    private Exception? failure;

    public void Initialize(IServiceProvider provider)
    {
        lock (sync)
        {
            if (initialized) return;
            if (failure is not null)
                throw new InvalidOperationException("Bus initialization previously failed. Rebuild the service provider before retrying.", failure);
            try
            {
                foreach (var action in provider.GetServices<IPostBuildAction>().OrderBy(action => action is ConsumerRegistrationAction ? 1 : 0))
                    action.Execute(provider);
                initialized = true;
            }
            catch (Exception exception)
            {
                failure = exception;
                throw;
            }
        }
    }
}
