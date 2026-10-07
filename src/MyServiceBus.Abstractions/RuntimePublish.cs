using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Reflection;

namespace MyServiceBus;

internal static class RuntimePublish
{
    private delegate Task Publisher(IPublishEndpoint endpoint, object message, Action<IPublishContext>? callback, CancellationToken token);
    private static readonly ConcurrentDictionary<Type, Publisher> Publishers = new();

    [RequiresDynamicCode("Runtime publication closes generic contracts dynamically. Use Publish<T> with known contracts for NativeAOT.")]
    [RequiresUnreferencedCode("Runtime publication needs preserved generic contract metadata.")]
    public static Task Publish(IPublishEndpoint endpoint, object message, Type type, Action<IPublishContext>? callback, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsValueType || type.ContainsGenericParameters || !type.IsInstanceOfType(message))
            throw new ArgumentException("Message must implement the selected closed reference contract.", nameof(type));
        var publisher = Publishers.GetOrAdd(type, t => typeof(RuntimePublish)
            .GetMethod(nameof(PublishTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(t).CreateDelegate<Publisher>());
        return publisher(endpoint, message, callback, token);
    }

    private static Task PublishTyped<T>(IPublishEndpoint endpoint, object message, Action<IPublishContext>? callback, CancellationToken token) where T : class
        => endpoint.Publish<T>((T)message, callback, token);
}
