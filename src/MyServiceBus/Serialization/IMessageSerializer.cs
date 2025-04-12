using System.Threading.Tasks;

namespace MyServiceBus.Serialization;

public interface IMessageSerializer
{
    [Throws(typeof(NotSupportedException))]
    Task<byte[]> SerializeAsync<T>(MessageSerializationContext<T> context)
        where T : class;
}