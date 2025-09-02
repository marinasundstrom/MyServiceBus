using System.Threading.Tasks;

namespace MyServiceBus;

public interface ISendPipe : IPipe<SendContext> { }

public interface IPublishPipe : IPipe<SendContext> { }

public class SendPipe : ISendPipe
{
    readonly IPipe<SendContext> pipe;
    public SendPipe(IPipe<SendContext> pipe) => this.pipe = pipe;
    public Task Send(SendContext context) => pipe.Send(context);
}

public class PublishPipe : IPublishPipe
{
    readonly IPipe<SendContext> pipe;
    public PublishPipe(IPipe<SendContext> pipe) => this.pipe = pipe;
    public Task Send(SendContext context) => pipe.Send(context);
}
