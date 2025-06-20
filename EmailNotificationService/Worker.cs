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
    private readonly IRabbitMqService _rabbitMqService;

    public Worker(
        ILogger<Worker> logger,
        IEmailService emailService,
        IRabbitMqService rabbitMqService)
    {
        _logger = logger;
        _emailService = emailService;
        _rabbitMqService = rabbitMqService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Notification Service started");

        // Start consuming email discrepancy messages
        _rabbitMqService.StartConsuming<EmailDiscrepancyNotification>("email.discrepancy", ProcessEmailNotification);

        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        _rabbitMqService.StopConsuming();
    }

    private async Task ProcessEmailNotification(EmailDiscrepancyNotification notification)
    {
        _logger.LogInformation("Processing email notification for file {FileId}", notification.FileId);

        try
        {
            await _emailService.SendDiscrepancyNotificationAsync(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification for file {FileId}", notification.FileId);
            throw; // Let RabbitMQ handle retry/DLQ
        }
    }
}