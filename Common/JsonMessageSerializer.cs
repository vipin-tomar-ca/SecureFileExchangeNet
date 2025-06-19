
using System.Text;
using System.Text.Json;

namespace SecureFileExchange.Common;

public class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonMessageSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public string ContentType => "application/json";

    public byte[] Serialize<T>(T message) where T : class
    {
        var json = JsonSerializer.Serialize(message, _options);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] data) where T : class
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(json, _options) 
               ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}");
    }
}
