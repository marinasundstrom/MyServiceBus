using System.Diagnostics;
using System.Threading.Tasks;

namespace MyServiceBus;

public static class MyServiceBusDiagnostics
{
    public static readonly ActivitySource ActivitySource = new("MyServiceBus");
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";
}

public class OpenTelemetrySendFilter : IFilter<SendContext>
{
    public async Task Send(SendContext context, IPipe<SendContext> next)
    {
        using var activity = MyServiceBusDiagnostics.ActivitySource.StartActivity("send", ActivityKind.Producer);
        if (activity != null)
        {
            context.Headers[MyServiceBusDiagnostics.TraceParent] = activity.Id;
            if (!string.IsNullOrEmpty(activity.TraceStateString))
                context.Headers[MyServiceBusDiagnostics.TraceState] = activity.TraceStateString;
        }

        await next.Send(context).ConfigureAwait(false);
    }
}

public class OpenTelemetryConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        ActivityContext parent = default;
        if (context is ConsumeContextImpl<T> ctx)
        {
            if (ctx.ReceiveContext.Headers.TryGetValue(MyServiceBusDiagnostics.TraceParent, out var tpObj) &&
                tpObj is string tp)
            {
                ctx.ReceiveContext.Headers.TryGetValue(MyServiceBusDiagnostics.TraceState, out var tsObj);
                ActivityContext.TryParse(tp, tsObj as string, out parent);
            }
        }

        using var activity = MyServiceBusDiagnostics.ActivitySource.StartActivity(
            "consume", ActivityKind.Consumer, parent);

        await next.Send(context).ConfigureAwait(false);
    }
}
