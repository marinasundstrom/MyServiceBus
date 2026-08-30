using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace MyServiceBus.Topology;

internal sealed class ReflectionUnionDescriptor
{
    private const string UnionAttributeName = "System.Runtime.CompilerServices.UnionAttribute";
    private const string UnionInterfaceName = "System.Runtime.CompilerServices.IUnion";
    private static readonly ConcurrentDictionary<Type, Lazy<ReflectionUnionDescriptor?>> Cache = new();
    private readonly IReadOnlyDictionary<Type, Func<object, object>> factories;

    private ReflectionUnionDescriptor(
        Type carrierType,
        IReadOnlyList<Type> caseTypes,
        IReadOnlyDictionary<Type, Func<object, object>> factories)
    {
        CarrierType = carrierType;
        CaseTypes = caseTypes;
        this.factories = factories;
    }

    public Type CarrierType { get; }

    public IReadOnlyList<Type> CaseTypes { get; }

    [RequiresDynamicCode("Reflection union factories compile constructor delegates at runtime.")]
    public static bool TryGet(Type type, [NotNullWhen(true)] out ReflectionUnionDescriptor? descriptor)
    {
        descriptor = Cache.GetOrAdd(
            type,
            static candidate => new Lazy<ReflectionUnionDescriptor?>(() => Discover(candidate))).Value;
        return descriptor is not null;
    }

    public Func<object, object> GetFactory(Type caseType)
        => factories.TryGetValue(caseType, out var factory)
            ? factory
            : throw new InvalidOperationException($"{caseType} is not a case of union {CarrierType}.");

    private static ReflectionUnionDescriptor? Discover(Type type)
    {
        var isUnion = type.CustomAttributes.Any(static attribute =>
            attribute.AttributeType.FullName == UnionAttributeName);
        if (!isUnion)
            return null;

        if (!type.GetInterfaces().Any(static implementedInterface =>
                implementedInterface.FullName == UnionInterfaceName))
        {
            throw new InvalidOperationException(
                $"Union carrier {type} is marked with UnionAttribute but does not implement IUnion.");
        }

        if (type.ContainsGenericParameters)
            throw new InvalidOperationException($"Union carrier {type} must be closed.");

        var valueProperty = type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueProperty?.PropertyType != typeof(object) || valueProperty.GetMethod is null)
        {
            throw new InvalidOperationException(
                $"Union carrier {type} must expose a public object Value property.");
        }

        var constructors = type
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(static constructor => constructor.GetParameters().Length == 1)
            .OrderBy(static constructor => constructor.MetadataToken)
            .ToArray();
        if (constructors.Length == 0)
        {
            throw new InvalidOperationException(
                $"Union carrier {type} must expose one public single-argument constructor per case.");
        }

        var caseTypes = constructors
            .Select(static constructor => constructor.GetParameters()[0].ParameterType)
            .ToArray();
        if (caseTypes.Distinct().Count() != caseTypes.Length)
            throw new InvalidOperationException($"Union carrier {type} contains duplicate case types.");

        var factories = constructors.ToDictionary(
            static constructor => constructor.GetParameters()[0].ParameterType,
            constructor => CreateFactory(type, constructor));
        return new ReflectionUnionDescriptor(type, caseTypes, factories);
    }

    [RequiresDynamicCode("Reflection union factories compile constructor delegates at runtime.")]
    private static Func<object, object> CreateFactory(Type carrierType, ConstructorInfo constructor)
    {
        var value = Expression.Parameter(typeof(object), "value");
        var caseType = constructor.GetParameters()[0].ParameterType;
        var create = Expression.New(constructor, Expression.Convert(value, caseType));
        return Expression.Lambda<Func<object, object>>(
            Expression.Convert(create, typeof(object)),
            value).Compile();
    }
}
