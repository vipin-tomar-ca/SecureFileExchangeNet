using SecureFileExchange.Common;
using SecureFileExchange.Services;
using SecureFileExchange.Contracts;

namespace SftpWorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ISftpService _sftpService;
    private readonly IRabbitMqService _rabbitMqService;
    private readonly IConfiguration _configuration;

    public Worker(ILogger<Worker> logger, ISftpService sftpService, 
                  IRabbitMqService rabbitMqService, IConfiguration configuration)
    {
        _logger = logger;
        _sftpService = sftpService;
        _rabbitMqService = rabbitMqService;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SFTP Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var vendors = _configuration.GetSection("VendorSettings:Vendors").Get<List<VendorConfig.VendorConfig>>() ?? new List<VendorConfig.VendorConfig>();

                foreach (var vendor in vendors)
                {
                    _logger.LogInformation("Polling SFTP for vendor {VendorId}", vendor.Id);

                    var files = await _sftpService.PollForFilesAsync(vendor.Id, stoppingToken);

                    foreach (var file in files)
                    {
                        await _rabbitMqService.PublishAsync("file.received", file, stoppingToken);
                        _logger.LogInformation("Published file received message for {FileId}", file.FileId);
                    }
                }

                var pollInterval = TimeSpan.FromSeconds(_configuration.GetValue<int>("SftpPollingIntervalSeconds", 300));
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SFTP polling");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}