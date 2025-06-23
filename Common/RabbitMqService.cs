using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
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
    private readonly IConfiguration _configuration;
    private readonly IMessageSerializer _serializer;
    private IConnection? _connection;
    private RabbitMQ.Client.IChannel _channel;
 // Add this namespace to resolve 'IModel'
    private readonly object _lock = new object();

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
            if (_connection?.IsOpen == true && _channel?.IsOpen == true) return;

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

            _connection?.Dispose();
            _channel?.Dispose();

            _connection = (IConnection?)factory.CreateConnectionAsync();
            _channel = (IChannel?)_connection.CreateChannelAsync();
        }
    }

    public Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default) where T : class
    {
        EnsureConnection();

        _channel!.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var body = _serializer.Serialize(message);
        // Replace the line causing the error with the following code to fix CS1061:

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = _serializer.ContentType
        };
        //var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = _serializer.ContentType;

        _channel.BasicPublishAsync(exchange: "", routingKey: queueName,true,properties, body: body);

        _logger.LogDebug("Published message to queue {QueueName}", queueName);
        return Task.CompletedTask;
    }

    public Task<T?> ConsumeAsync<T>(string queueName, CancellationToken cancellationToken = default) where T : class
    {
        EnsureConnection();

        _channel!.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var result = _channel.BasicGetAsync(queueName, autoAck: false);
        if (result == null) return Task.FromResult<T?>(null);

        try
        {
            var message = _serializer.Deserialize<T>(result.Result.Body.ToArray());
            _channel.BasicAckAsync(result.Result.DeliveryTag, false);
            return Task.FromResult<T?>(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize message from queue {QueueName}", queueName);
            _channel.BasicNackAsync(result.Result.DeliveryTag, false, false); // Send to DLQ
            return Task.FromResult<T?>(null);
        }
    }

    // Fix for CS1061: Ensure that _channel is not null before calling BasicConsume
    public void StartConsuming<T>(string queueName, Func<T, Task> handler) where T : class
    {
        EnsureConnection();

        if (_channel == null)
        {
            _logger.LogError("Channel is not initialized. Cannot start consuming from queue {QueueName}", queueName);
            throw new InvalidOperationException("Channel is not initialized.");
        }

        _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var message = _serializer.Deserialize<T>(ea.Body.ToArray());
                await handler(message);
                _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue {QueueName}", queueName);
                _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        };

        _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);
        _logger.LogInformation("Started consuming from queue {QueueName}", queueName);
    }

    public void StopConsuming()
    {
        _channel?.CloseAsync();
        _channel?.Dispose();
        _channel = null;
        _connection?.CloseAsync();
        _connection?.Dispose();
        _connection = null;
    }

    public void Dispose()
    {
        StopConsuming();
    }
}