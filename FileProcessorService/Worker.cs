using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using SecureFileExchange.Contracts;
using System.Text;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IFileProcessorService _fileProcessorService;
    private readonly IRabbitMqService _rabbitMqService;

    public Worker(
        ILogger<Worker> logger,
        IFileProcessorService fileProcessorService,
        IRabbitMqService rabbitMqService)
    {
        _logger = logger;
        _fileProcessorService = fileProcessorService;
        _rabbitMqService = rabbitMqService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("File Processor Service started");

        // Start consuming file received messages
        _rabbitMqService.StartConsuming<FileReceivedMessage>("file.received", ProcessFileMessage);

        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        _rabbitMqService.StopConsuming();
    }

    private async Task ProcessFileMessage(FileReceivedMessage message)
    {
        _logger.LogInformation("Processing file message for {FileId}", message.FileId);

        try
        {
            await _fileProcessorService.ProcessFileAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file {FileId}", message.FileId);
            throw; // Let RabbitMQ handle retry/DLQ
        }
    }
}