using RabbitMQ.Client;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace SecureFileExchange.Common;

public interface IRabbitMqService
{
    Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default) where T : class;
    Task<T?> ConsumeAsync<T>(string queueName, CancellationToken cancellationToken = default) where T : class;
    void StartConsuming<T>(string queueName, Func<T, Task> handler) where T : class;
    void StopConsuming();
}

public class RabbitMqService : IRabbitMqService, IDisposable
{
    private readonly ILogger<RabbitMqService> _logger;
    private readonly IMessageSerializer _serializer;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new object();
    private readonly IConfiguration _configuration;

    public RabbitMqService(ILogger<RabbitMqService> logger, IConfiguration configuration, IMessageSerializer serializer)
    {
        _logger = logger;
        _configuration = configuration;
        _serializer = serializer;
    }

    private void EnsureConnection()
    {
        lock (_lock)
        {
            if (_connection?.IsOpen == true) return;

            var factory = new ConnectionFactory
            {
                HostName = _configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost",
                Port = _configuration.GetValue<int>("RabbitMQ:Port", 5672),
                UserName = _configuration.GetValue<string>("RabbitMQ:Username") ?? "guest",
                Password = _configuration.GetValue<string>("RabbitMQ:Password") ?? "guest",
                VirtualHost = _configuration.GetValue<string>("RabbitMQ:VirtualHost") ?? "/",
                Ssl = new SslOption
                {
                    Enabled = _configuration.GetValue<bool>("RabbitMQ:UseSsl", false)
                }
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }
    }

    public async Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default) where T : class
    {
        EnsureConnection();

        _channel!.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var body = _serializer.Serialize(message);
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = _serializer.ContentType;

        _channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: body);

        _logger.LogDebug("Published message to queue {QueueName}", queueName);
    }

    public async Task<T?> ConsumeAsync<T>(string queueName, CancellationToken cancellationToken = default) where T : class
    {
        EnsureConnection();

        _channel!.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var result = _channel.BasicGet(queueName, autoAck: false);
        if (result == null) return null;

        try
        {
            var message = _serializer.Deserialize<T>(result.Body.ToArray());
            _channel.BasicAck(result.DeliveryTag, false);
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize message from queue {QueueName}", queueName);
            _channel.BasicNack(result.DeliveryTag, false, false); // Send to DLQ
            return null;
        }
    }

    public void StartConsuming<T>(string queueName, Func<T, Task> handler) where T : class
    {
        EnsureConnection();

        _channel!.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var message = _serializer.Deserialize<T>(ea.Body.ToArray());
                await handler(message);
                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue {QueueName}", queueName);
                _channel.BasicNack(ea.DeliveryTag, false, false);
            }
        };

        _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        _logger.LogInformation("Started consuming from queue {QueueName}", queueName);
    }

    public void StopConsuming()
    {
        _channel?.Close();
        _connection?.Close();
    }

    public void Dispose()
    {
        StopConsuming();
    }
}