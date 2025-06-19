
using Google.Protobuf;

namespace SecureFileExchange.Common;

public class ProtobufMessageSerializer : IMessageSerializer
{
    public string ContentType => "application/x-protobuf";

    public byte[] Serialize<T>(T message) where T : class
    {
        if (message is IMessage protobufMessage)
        {
            return protobufMessage.ToByteArray();
        }
        
        throw new ArgumentException($"Type {typeof(T).Name} is not a protobuf message");
    }

    public T Deserialize<T>(byte[] data) where T : class
    {
        if (typeof(T).IsAssignableTo(typeof(IMessage)))
        {
            var parser = GetParser<T>();
            return (T)parser.ParseFrom(data);
        }
        
        throw new ArgumentException($"Type {typeof(T).Name} is not a protobuf message");
    }

    private static MessageParser GetParser<T>() where T : class
    {
        var parserProperty = typeof(T).GetProperty("Parser", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (parserProperty?.GetValue(null) is MessageParser parser)
        {
            return parser;
        }
        
        throw new InvalidOperationException($"No parser found for type {typeof(T).Name}");
    }
}
