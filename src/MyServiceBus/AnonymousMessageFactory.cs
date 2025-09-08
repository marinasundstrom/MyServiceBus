using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MyServiceBus;

public static class AnonymousMessageFactory
{
    public static T Create<T>(object values) where T : class
    {
        return values as T ?? InterfaceProxy.Create<T>(values);
    }

    private static class InterfaceProxy
    {
        public static T Create<T>(object source) where T : class
        {
            var proxy = DispatchProxy.Create<T, PropertyMappingDispatchProxy>();
            ((PropertyMappingDispatchProxy)(object)proxy).Initialize(source);
            return proxy;
        }
    }

    private class PropertyMappingDispatchProxy : DispatchProxy
    {
        object _source = null!;
        Dictionary<string, PropertyInfo> _properties = null!;

        [Throws(typeof(TargetInvocationException))]
        protected override object? Invoke(MethodInfo targetMethod, object?[]? args)
        {
            if (targetMethod.IsSpecialName && targetMethod.Name.StartsWith("get_", StringComparison.Ordinal))
            {
                var name = targetMethod.Name[4..];
                if (_properties.TryGetValue(name, out var prop))
                    return prop.GetValue(_source);

                return targetMethod.ReturnType.IsValueType
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null;
            }

            throw new NotImplementedException(targetMethod.Name);
        }

        public void Initialize(object source)
        {
            _source = source;
            _properties = source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .ToDictionary(p => p.Name);
        }
    }
}
