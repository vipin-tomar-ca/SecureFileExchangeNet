
using System.Text.Json;
using SecureFileExchange.Contracts;

namespace SecureFileExchange.Services;

public class FileProcessorService : IFileProcessorService
{
    private readonly ILogger<FileProcessorService> _logger;
    private readonly BusinessRulesService.BusinessRulesServiceClient _businessRulesClient;

    public FileProcessorService(ILogger<FileProcessorService> logger, BusinessRulesService.BusinessRulesServiceClient businessRulesClient)
    {
        _logger = logger;
        _businessRulesClient = businessRulesClient;
    }

    public async Task ProcessFileAsync(FileReceivedMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing file {FileId} for vendor {VendorId} with correlation ID {CorrelationId}", 
                message.FileId, message.VendorId, message.CorrelationId);

            var records = await ParseFileAsync(message.FilePath, message.VendorId, cancellationToken);

            var validationRequest = new ValidateRecordsRequest
            {
                VendorId = message.VendorId,
                CorrelationId = message.CorrelationId
            };
            validationRequest.Records.AddRange(records);

            var validationResult = await _businessRulesClient.ValidateRecordsAsync(validationRequest, cancellationToken: cancellationToken);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning("File {FileId} validation failed with {DiscrepancyCount} discrepancies", 
                    message.FileId, validationResult.Discrepancies.Count);

                // TODO: Publish email notification message to RabbitMQ
            }
            else
            {
                _logger.LogInformation("File {FileId} processed successfully", message.FileId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file {FileId} with correlation ID {CorrelationId}", 
                message.FileId, message.CorrelationId);
        }
    }

    public async Task<List<FileRecord>> ParseFileAsync(string filePath, string vendorId, CancellationToken cancellationToken = default)
    {
        var records = new List<FileRecord>();

        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            switch (extension)
            {
                case ".json":
                    records = await ParseJsonFileAsync(filePath, cancellationToken);
                    break;
                case ".csv":
                    records = await ParseCsvFileAsync(filePath, cancellationToken);
                    break;
                case ".xml":
                    records = await ParseXmlFileAsync(filePath, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"File format {extension} is not supported");
            }

            _logger.LogInformation("Parsed {RecordCount} records from file {FilePath}", records.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing file {FilePath}", filePath);
        }

        return records;
    }

    private async Task<List<FileRecord>> ParseJsonFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var jsonData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content);
        
        return jsonData?.Select((item, index) => new FileRecord
        {
            RecordId = index.ToString(),
            Fields = { item.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty) }
        }).ToList() ?? new List<FileRecord>();
    }

    private async Task<List<FileRecord>> ParseCsvFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var records = new List<FileRecord>();
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        
        if (lines.Length == 0) return records;

        var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        
        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',').Select(v => v.Trim()).ToArray();
            var fields = new Dictionary<string, string>();
            
            for (int j = 0; j < Math.Min(headers.Length, values.Length); j++)
            {
                fields[headers[j]] = values[j];
            }

            records.Add(new FileRecord
            {
                RecordId = i.ToString(),
                Fields = { fields }
            });
        }

        return records;
    }

    private async Task<List<FileRecord>> ParseXmlFileAsync(string filePath, CancellationToken cancellationToken)
    {
        // Simple XML parsing implementation - would need more sophisticated parsing for complex XML
        var records = new List<FileRecord>();
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        
        // TODO: Implement XML parsing logic based on vendor-specific schema
        _logger.LogWarning("XML parsing not fully implemented yet for file {FilePath}", filePath);
        
        return records;
    }
}
