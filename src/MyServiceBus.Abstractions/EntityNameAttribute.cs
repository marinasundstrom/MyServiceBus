using System;

namespace MyServiceBus;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EntityNameAttribute : Attribute
{
    public EntityNameAttribute(string entityName)
    {
        EntityName = entityName;
    }

    public string EntityName { get; }
}
