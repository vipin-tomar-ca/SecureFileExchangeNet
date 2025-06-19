
using SecureFileExchange.Services;

namespace SftpWorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ISftpService _sftpService;

    public Worker(ILogger<Worker> logger, ISftpService sftpService)
    {
        _logger = logger;
        _sftpService = sftpService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("SFTP Worker running at: {time}", DateTimeOffset.Now);
                
                // Poll all configured vendors
                await _sftpService.PollAllVendorsAsync(stoppingToken);
                
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in SFTP Worker");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
using RabbitMQ.Client;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;
using Microsoft.Extensions.Options;
using System.Text;

namespace SftpWorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ISftpService _sftpService;
    private readonly IMessageSerializer _serializer;
    private readonly VendorSettings _vendorSettings;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public Worker(ILogger<Worker> logger, ISftpService sftpService, 
                  IMessageSerializer serializer, IOptions<VendorSettings> vendorSettings)
    {
        _logger = logger;
        _sftpService = sftpService;
        _serializer = serializer;
        _vendorSettings = vendorSettings.Value;

        var factory = new ConnectionFactory() { HostName = "localhost" };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        
        _channel.QueueDeclare(queue: "file.received", durable: true, exclusive: false, autoDelete: false, arguments: null);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var vendor in _vendorSettings.Vendors)
                {
                    var files = await _sftpService.PollForFilesAsync(vendor.Id, stoppingToken);
                    
                    foreach (var fileInfo in files)
                    {
                        var message = new FileReceivedMessage
                        {
                            FileId = Guid.NewGuid().ToString(),
                            VendorId = vendor.Id,
                            FilePath = fileInfo.Path,
                            FileHash = fileInfo.Hash,
                            FileSize = fileInfo.Size,
                            ReceivedAt = DateTime.UtcNow.ToString("O"),
                            CorrelationId = Guid.NewGuid().ToString()
                        };

                        var body = _serializer.Serialize(message);
                        _channel.BasicPublish(exchange: "", routingKey: "file.received", basicProperties: null, body: body);
                        
                        _logger.LogInformation("Published file received message for {VendorId}: {FileId}", vendor.Id, message.FileId);
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SFTP polling");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
