using System.Threading.Tasks;

namespace MyServiceBus;

public interface ISendPipe : IPipe<SendContext> { }

public interface IPublishPipe : IPipe<PublishContext> { }

public class SendPipe : ISendPipe
{
    readonly IPipe<SendContext> pipe;
    public SendPipe(IPipe<SendContext> pipe) => this.pipe = pipe;
    public Task Send(SendContext context) => pipe.Send(context);
}

public class PublishPipe : IPublishPipe
{
    readonly IPipe<PublishContext> pipe;
    public PublishPipe(IPipe<PublishContext> pipe) => this.pipe = pipe;
    public Task Send(PublishContext context) => pipe.Send(context);
}
