using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MyServiceBus;

/// <summary>
/// Maps an anonymous object to a specified interface by using a <see cref="DispatchProxy"/>.
/// </summary>
public static class MessageProxy
{
    class PropertyMappingDispatchProxy : DispatchProxy
    {
        object _source = null!;
        Dictionary<string, PropertyInfo> _properties = null!;

        [Throws(typeof(TargetInvocationException), typeof(MethodAccessException), typeof(InvalidComObjectException), typeof(MissingMethodException), typeof(COMException), typeof(TypeLoadException))]
        protected override object? Invoke(MethodInfo targetMethod, object?[]? args)
        {
            if (targetMethod.IsSpecialName && targetMethod.Name.StartsWith("get_"))
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
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static object Create(Type interfaceType, object source)
    {
        var proxy = (PropertyMappingDispatchProxy)DispatchProxy.Create(interfaceType, typeof(PropertyMappingDispatchProxy));
        proxy.Initialize(source);
        return proxy;
    }

    public static T Create<T>(object source) where T : class
        => (T)Create(typeof(T), source);
}

