
namespace SecureFileExchange.Common;

public interface IMessageSerializer
{
    byte[] Serialize<T>(T message) where T : class;
    T Deserialize<T>(byte[] data) where T : class;
    string ContentType { get; }
}
