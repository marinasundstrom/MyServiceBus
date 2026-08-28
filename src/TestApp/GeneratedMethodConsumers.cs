using MyServiceBus;

namespace TestApp;

public sealed class GeneratedConsumerAudit
{
    public GeneratedMethodMessage? Message { get; private set; }

    public ConsumeContext<GeneratedMethodMessage>? Context { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public GeneratedClassMethodMessage? ClassMessage { get; private set; }

    public ConsumeContext<GeneratedClassMethodMessage>? ClassContext { get; private set; }

    public void Record(
        GeneratedMethodMessage message,
        ConsumeContext<GeneratedMethodMessage> context,
        CancellationToken cancellationToken)
    {
        Message = message;
        Context = context;
        CancellationToken = cancellationToken;
    }

    public void RecordClass(
        GeneratedClassMethodMessage message,
        ConsumeContext<GeneratedClassMethodMessage> context)
    {
        ClassMessage = message;
        ClassContext = context;
    }
}

public sealed record GeneratedMethodMessage(string Value);

public sealed record GeneratedGroupedMethodMessage(string Value);

public sealed record GeneratedClassMethodMessage(string Value);

public sealed record GeneratedConventionMethodMessage(string Value);

public sealed record GeneratedResponseRequest(string Value);

public sealed record GeneratedResponse(string Value);

public sealed record GeneratedValueTaskResponseRequest(string Value);

public sealed record GeneratedValueTaskResponse(string Value);

[Consumer("generated-methods")]
public static class GeneratedMethodConsumers
{
    public static Task ReceiveGrouped(GeneratedGroupedMethodMessage message)
        => Task.CompletedTask;

    public static ValueTask ReceiveTestRequest(
        GeneratedMethodMessage message,
        ConsumeContext<GeneratedMethodMessage> context,
        GeneratedConsumerAudit audit,
        CancellationToken cancellationToken)
    {
        audit.Record(message, context, cancellationToken);
        return ValueTask.CompletedTask;
    }
}

public static class MethodAttributedConsumers
{
    [Consumer("generated-response")]
    public static Task<GeneratedResponse> Respond(GeneratedResponseRequest request)
        => Task.FromResult(new GeneratedResponse($"{request.Value}-response"));

    [Consumer("generated-value-task-response")]
    public static ValueTask<GeneratedValueTaskResponse> RespondValueTask(GeneratedValueTaskResponseRequest request)
        => ValueTask.FromResult(new GeneratedValueTaskResponse($"{request.Value}-response"));

    [Consumer("generated-class-method")]
    public static Task Receive(
        GeneratedClassMethodMessage message,
        ConsumeContext<GeneratedClassMethodMessage> context,
        GeneratedConsumerAudit audit)
    {
        audit.RecordClass(message, context);
        return Task.CompletedTask;
    }

    [Consumer]
    public static void ObserveConvention(GeneratedConventionMethodMessage message)
    {
    }
}
