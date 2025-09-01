using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyServiceBus;

public class Fault<T>
    where T : class
{
    [JsonPropertyName("message")] public T Message { get; set; }

    [JsonPropertyName("exceptions")] public List<ExceptionInfo> Exceptions { get; set; } = new();

    [JsonPropertyName("host")] public HostInfo? Host { get; set; }

    [JsonPropertyName("messageId")] public Guid? MessageId { get; set; }

    [JsonPropertyName("sentTime")] public DateTimeOffset SentTime { get; set; }

    [JsonPropertyName("faultId")] public Guid FaultId { get; set; }

    [JsonPropertyName("conversationId")] public Guid? ConversationId { get; set; }

    [JsonPropertyName("correlationId")] public Guid? CorrelationId { get; set; }
}

public class ExceptionInfo
{
    [JsonPropertyName("exceptionType")] public string ExceptionType { get; set; } = string.Empty;

    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;

    [JsonPropertyName("stackTrace")] public string? StackTrace { get; set; }

    [JsonPropertyName("innerException")] public ExceptionInfo? InnerException { get; set; }

    public static ExceptionInfo FromException(Exception exception) => new ExceptionInfo
    {
        ExceptionType = exception.GetType().FullName ?? string.Empty,
        Message = exception.Message,
        StackTrace = exception.StackTrace,
        InnerException = exception.InnerException != null ? FromException(exception.InnerException) : null
    };
}
