using System;
using System.Reflection;

namespace MyServiceBus;

public static class AnonymousMessageFactory
{
    public static T Create<T>(object values) where T : class
    {
        if (values is T typed)
            return typed;

        var proxy = DispatchProxy.Create<T, AnonymousProxy<T>>();
        ((AnonymousProxy<T>)(object)proxy!).Init(values);
        return proxy!;
    }

    class AnonymousProxy<T> : DispatchProxy where T : class
    {
        object? _values;

        public void Init(object values) => _values = values;

        [Throws(typeof(AmbiguousMatchException))]
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null || _values == null)
                return null;

            if (targetMethod.IsSpecialName && targetMethod.Name.StartsWith("get_", StringComparison.Ordinal))
            {
                var name = targetMethod.Name[4..];
                var property = _values.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                return property?.GetValue(_values);
            }

            throw new NotImplementedException($"Method {targetMethod.Name} is not implemented");
        }
    }
}
