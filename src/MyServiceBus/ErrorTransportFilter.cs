using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MyServiceBus;

public class ErrorTransportFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    private readonly ILogger<ErrorTransportFilter<TMessage>>? logger;

    public ErrorTransportFilter(ILogger<ErrorTransportFilter<TMessage>>? logger = null)
    {
        this.logger = logger;
    }

    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        try
        {
            await next.Send(context);
        }
        catch (Exception ex)
        {
            if (context is ConsumeContextImpl<TMessage> ctx)
            {
                var errorAddress = ctx.ReceiveContext.ErrorAddress;
                if (errorAddress != null)
                {
                    var endpoint = await ctx.GetSendEndpoint(errorAddress);
                    var headers = ctx.ReceiveContext.Headers;
                    int redeliveryCount = 0;
                    if (headers.TryGetValue(MessageHeaders.RedeliveryCount, out var value))
                    {
                        if (!int.TryParse(value?.ToString(), out redeliveryCount))
                        {
                            logger?.LogWarning("Malformed RedeliveryCount header: {HeaderValue}", value);
                            redeliveryCount = 0;
                        }
                    }

                    await endpoint.Send(ctx.Message, [Throws(typeof(NotSupportedException))] (sendCtx) =>
                    {
                        sendCtx.Headers[MessageHeaders.ExceptionType] = ex.GetType().FullName ?? ex.GetType().Name;
                        sendCtx.Headers[MessageHeaders.ExceptionMessage] = ex.Message;
                        sendCtx.Headers[MessageHeaders.ExceptionStackTrace] = ex.StackTrace ?? string.Empty;
                        sendCtx.Headers[MessageHeaders.Reason] = "fault";
                        sendCtx.Headers[MessageHeaders.RedeliveryCount] = redeliveryCount;
                        sendCtx.Headers[MessageHeaders.HostMachineName] = Environment.MachineName;
                        sendCtx.Headers[MessageHeaders.HostProcessName] = Environment.ProcessPath ?? "unknown";
                        sendCtx.Headers[MessageHeaders.HostProcessId] = Environment.ProcessId;
                        sendCtx.Headers[MessageHeaders.HostAssembly] = typeof(TMessage).Assembly.GetName().Name ?? "unknown";
                        sendCtx.Headers[MessageHeaders.HostAssemblyVersion] = typeof(TMessage).Assembly.GetName().Version?.ToString() ?? "unknown";
                        sendCtx.Headers[MessageHeaders.HostFrameworkVersion] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
                        sendCtx.Headers[MessageHeaders.HostMassTransitVersion] = typeof(MessageBus).Assembly.GetName().Version?.ToString() ?? "unknown";
                        sendCtx.Headers[MessageHeaders.HostOperatingSystemVersion] = Environment.OSVersion.VersionString;
                    }, context.CancellationToken);
                }
            }

            throw;
        }
    }
}
