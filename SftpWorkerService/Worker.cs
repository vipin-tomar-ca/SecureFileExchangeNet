
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using SecureFileExchange.Contracts;
using Microsoft.Extensions.Options;
using SecureFileExchange.VendorConfig;

namespace SftpWorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ISftpService _sftpService;
    private readonly IRabbitMqService _rabbitMqService;
    private readonly VendorSettings _vendorSettings;

    public Worker(
        ILogger<Worker> logger,
        ISftpService sftpService,
        IRabbitMqService rabbitMqService,
        IOptions<VendorSettings> vendorSettings)
    {
        _logger = logger;
        _sftpService = sftpService;
        _rabbitMqService = rabbitMqService;
        _vendorSettings = vendorSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SFTP Worker Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var vendor in _vendorSettings.Vendors)
                {
                    await ProcessVendorFiles(vendor, stoppingToken);
                }

                // Wait for the shortest poll interval among all vendors
                var minInterval = _vendorSettings.Vendors.Any() 
                    ? _vendorSettings.Vendors.Min(v => v.SftpSettings.PollIntervalSeconds)
                    : 300;
                
                await Task.Delay(TimeSpan.FromSeconds(minInterval), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SFTP worker service");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); // Wait 1 minute on error
            }
        }
    }

    private async Task ProcessVendorFiles(VendorConfiguration vendor, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Polling files for vendor {VendorId}", vendor.Id);
            
            var files = await _sftpService.PollForFilesAsync(vendor.Id, cancellationToken);
            
            foreach (var file in files)
            {
                await _rabbitMqService.PublishAsync("file.received", file, cancellationToken);
                _logger.LogInformation("Published file received message for {FileId}", file.FileId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process files for vendor {VendorId}", vendor.Id);
        }
    }
}
