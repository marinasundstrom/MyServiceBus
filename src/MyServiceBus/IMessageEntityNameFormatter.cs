using System;

namespace MyServiceBus;

public interface IMessageEntityNameFormatter
{
    string FormatEntityName(Type messageType);
}

public interface IMessageEntityNameFormatter<T>
{
    string FormatEntityName();
}
