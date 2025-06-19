
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using SecureFileExchange.Contracts;
using System.Text;

namespace FileProcessorService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IFileProcessorService _fileProcessorService;
    private readonly IRabbitMqService _rabbitMqService;
    private readonly IMessageSerializer _messageSerializer;
    private IConnection? _connection;
    private IModel? _channel;

    public Worker(ILogger<Worker> logger, IFileProcessorService fileProcessorService,
                  IRabbitMqService rabbitMqService, IMessageSerializer messageSerializer)
    {
        _logger = logger;
        _fileProcessorService = fileProcessorService;
        _rabbitMqService = rabbitMqService;
        _messageSerializer = messageSerializer;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = await _rabbitMqService.GetConnectionAsync();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(queue: "file.received", durable: true, exclusive: false, autoDelete: false);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var message = _messageSerializer.Deserialize<FileReceivedMessage>(ea.Body.ToArray());
                await _fileProcessorService.ProcessFileAsync(message, cancellationToken);
                
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file received message");
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(queue: "file.received", autoAck: false, consumer: consumer);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("File Processor Worker started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
