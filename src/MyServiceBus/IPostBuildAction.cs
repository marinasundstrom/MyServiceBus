namespace MyServiceBus;

public interface IPostBuildAction
{
    void Execute(IServiceProvider provider);
}
