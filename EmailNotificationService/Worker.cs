
using SecureFileExchange.Contracts;
using SecureFileExchange.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SecureFileExchange.Common;
using System.Text;

namespace EmailNotificationService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IEmailService _emailService;
    private readonly IMessageSerializer _messageSerializer;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IModel? _channel;

    public Worker(ILogger<Worker> logger, IEmailService emailService, 
                  IMessageSerializer messageSerializer, IConfiguration configuration)
    {
        _logger = logger;
        _emailService = emailService;
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

        _channel.QueueDeclare(queue: "email.discrepancy", durable: true, exclusive: false, autoDelete: false);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var notification = _messageSerializer.Deserialize<EmailDiscrepancyNotification>(body);
                
                await _emailService.SendDiscrepancyNotificationAsync(notification, stoppingToken);
                
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                _logger.LogInformation("Processed discrepancy notification for file {FileId}", notification.FileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing discrepancy notification");
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(queue: "email.discrepancy", autoAck: false, consumer: consumer);

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
