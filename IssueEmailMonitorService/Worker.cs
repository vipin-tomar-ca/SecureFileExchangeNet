
using RabbitMQ.Client;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using SecureFileExchange.Contracts;

namespace IssueEmailMonitorService;

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

        _channel.QueueDeclare(queue: "thirdparty.issue", durable: true, exclusive: false, autoDelete: false);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Poll email inboxes for all vendors
                var vendors = _configuration.GetSection("Vendors").GetChildren();
                
                foreach (var vendor in vendors)
                {
                    var vendorId = vendor.Key;
                    var issues = await _emailService.PollEmailInboxAsync(vendorId, stoppingToken);
                    
                    foreach (var issue in issues)
                    {
                        var messageBody = _messageSerializer.Serialize(issue);
                        _channel?.BasicPublish(exchange: "", routingKey: "thirdparty.issue", body: messageBody);
                        _logger.LogInformation("Published third-party issue for vendor {VendorId}", vendorId);
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring email inbox");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        await base.StopAsync(cancellationToken);
    }
}
