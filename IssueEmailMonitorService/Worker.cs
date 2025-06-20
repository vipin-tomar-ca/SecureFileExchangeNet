
using RabbitMQ.Client;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using SecureFileExchange.Contracts;
using Microsoft.Extensions.Options;
using SecureFileExchange.VendorConfig;

namespace IssueEmailMonitorService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IEmailService _emailService;
    private readonly IRabbitMqService _rabbitMqService;
    private readonly VendorSettings _vendorSettings;

    public Worker(
        ILogger<Worker> logger,
        IEmailService emailService,
        IRabbitMqService rabbitMqService,
        IOptions<VendorSettings> vendorSettings)
    {
        _logger = logger;
        _emailService = emailService;
        _rabbitMqService = rabbitMqService;
        _vendorSettings = vendorSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Issue Email Monitor Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var vendor in _vendorSettings.Vendors)
                {
                    await PollVendorEmails(vendor, stoppingToken);
                }

                // Poll every 5 minutes
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Issue Email Monitor Service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task PollVendorEmails(VendorConfiguration vendor, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Polling emails for vendor {VendorId}", vendor.Id);
            
            var issues = await _emailService.PollEmailInboxAsync(vendor.Id, cancellationToken);
            
            foreach (var issue in issues)
            {
                await _rabbitMqService.PublishAsync("issue.reported", issue, cancellationToken);
                _logger.LogInformation("Published issue report for vendor {VendorId}: {Subject}", 
                    vendor.Id, issue.EmailSubject);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll emails for vendor {VendorId}", vendor.Id);
        }
    }
}
