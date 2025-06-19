
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SecureFileExchange.Common;
using SecureFileExchange.Contracts;
using SecureFileExchange.Services;
using System.Text;

namespace FileProcessorService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IFileProcessorService _fileProcessorService;
    private readonly IMessageSerializer _messageSerializer;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IModel? _channel;

    public Worker(ILogger<Worker> logger, IFileProcessorService fileProcessorService,
                  IMessageSerializer messageSerializer, IConfiguration configuration)
    {
        _logger = logger;
        _fileProcessorService = fileProcessorService;
        _messageSerializer = messageSerializer;
        _configuration = configuration;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory()
        {
            HostName = _configuration["RabbitMQ:Host"],
            Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = _configuration["RabbitMQ:Username"],
            Password = _configuration["RabbitMQ:Password"]
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(queue: "file.received", durable: true, exclusive: false, autoDelete: false);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            try
            {
                var message = _messageSerializer.Deserialize<FileReceivedMessage>(body);
                await _fileProcessorService.ProcessFileAsync(message, stoppingToken);
                _channel?.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file message");
                _channel?.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel?.BasicConsume(queue: "file.received", autoAck: false, consumer: consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        await base.StopAsync(cancellationToken);
    }
}
