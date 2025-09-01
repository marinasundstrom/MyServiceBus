using System.Collections.Generic;

namespace System;

[AttributeUsage(
    AttributeTargets.Method |
    AttributeTargets.Constructor |
    AttributeTargets.Delegate |
    AttributeTargets.Property,
    AllowMultiple = true)]
public sealed class ThrowsAttribute : Attribute
{
    public List<Type> ExceptionTypes { get; } = new();

    public ThrowsAttribute(Type exceptionType, params Type[] exceptionTypes)
    {
        if (!typeof(Exception).IsAssignableFrom(exceptionType))
#pragma warning disable THROW001 // Unhandled exception
            throw new ArgumentException("Must be an Exception type.");
#pragma warning restore THROW001 // Unhandled exception

        ExceptionTypes.Add(exceptionType);

        foreach (var type in exceptionTypes)
        {
            if (!typeof(Exception).IsAssignableFrom(type))
#pragma warning disable THROW001 // Unhandled exception
                throw new ArgumentException("Must be an Exception type.");
#pragma warning restore THROW001 // Unhandled exception

            ExceptionTypes.Add(type);
        }
    }
}

