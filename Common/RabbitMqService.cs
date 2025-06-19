
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace SecureFileExchange.Common;

public interface IRabbitMqService
{
    Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default) where T : class;
    Task<IConnection> GetConnectionAsync();
    Task<IModel> GetChannelAsync();
}

public class RabbitMqService : IRabbitMqService, IDisposable
{
    private readonly ILogger<RabbitMqService> _logger;
    private readonly IMessageSerializer _messageSerializer;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqService(ILogger<RabbitMqService> logger, IMessageSerializer messageSerializer, IConfiguration configuration)
    {
        _logger = logger;
        _messageSerializer = messageSerializer;
        _configuration = configuration;
    }

    public async Task<IConnection> GetConnectionAsync()
    {
        if (_connection == null || !_connection.IsOpen)
        {
            var factory = new ConnectionFactory()
            {
                HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = _configuration["RabbitMQ:Username"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest",
                VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/",
                Ssl = new SslOption
                {
                    Enabled = bool.Parse(_configuration["RabbitMQ:UseTls"] ?? "false")
                }
            };

            _connection = await Task.Run(() => factory.CreateConnection());
        }

        return _connection;
    }

    public async Task<IModel> GetChannelAsync()
    {
        if (_channel == null || _channel.IsClosed)
        {
            var connection = await GetConnectionAsync();
            _channel = connection.CreateModel();
        }

        return _channel;
    }

    public async Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var channel = await GetChannelAsync();
            
            channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);

            var messageBytes = _messageSerializer.Serialize(message);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = _messageSerializer.ContentType;

            channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: messageBytes);

            _logger.LogInformation("Published message to queue {QueueName}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to queue {QueueName}", queueName);
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
