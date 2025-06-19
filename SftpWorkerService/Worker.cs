
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
