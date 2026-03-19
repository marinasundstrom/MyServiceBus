using System.Threading.Tasks;

namespace MyServiceBus.Serialization;

public interface IMessageSerializer
{
    Task<byte[]> SerializeAsync<T>(MessageSerializationContext<T> context)
        where T : class;
}