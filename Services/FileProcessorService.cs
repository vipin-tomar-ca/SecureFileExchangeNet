using System.Text.Json;
using System.Text;
using System.Xml;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Grpc.Net.Client;
using SecureFileExchange.Common;

namespace SecureFileExchange.Services;

public class FileProcessorService : IFileProcessorService
{
    private readonly ILogger<FileProcessorService> _logger;
    private readonly VendorSettings _vendorSettings;
    private readonly IRabbitMqService _rabbitMqService;
    private readonly IEncryptionService _encryptionService;

    public FileProcessorService(
        ILogger<FileProcessorService> logger,
        IOptions<VendorSettings> vendorSettings,
        IRabbitMqService rabbitMqService,
        IEncryptionService encryptionService)
    {
        _logger = logger;
        _vendorSettings = vendorSettings.Value;
        _rabbitMqService = rabbitMqService;
        _encryptionService = encryptionService;
    }

    public async Task ProcessFileAsync(FileReceivedMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing file {FileId} for vendor {VendorId}", message.FileId, message.VendorId);

        try
        {
            // Parse the file
            var records = await ParseFileAsync(message.FilePath, message.VendorId, cancellationToken);

            // Validate records using Business Rules gRPC service
            var validationResult = await ValidateRecordsAsync(message.VendorId, records, message.CorrelationId, cancellationToken);

            if (!validationResult.IsValid)
            {
                // Send email notification for discrepancies
                var emailNotification = new EmailDiscrepancyNotification
                {
                    VendorId = message.VendorId,
                    FileId = message.FileId,
                    CorrelationId = message.CorrelationId
                };
                emailNotification.Discrepancies.AddRange(validationResult.Discrepancies);

                await _rabbitMqService.PublishAsync("email.discrepancy", emailNotification, cancellationToken);
                _logger.LogWarning("File {FileId} has {Count} validation discrepancies", message.FileId, validationResult.Discrepancies.Count);
            }

            // Archive the processed file
            await ArchiveFileAsync(message, validationResult, cancellationToken);

            _logger.LogInformation("Successfully processed file {FileId}", message.FileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file {FileId}", message.FileId);
            throw;
        }
    }

    public async Task<List<FileRecord>> ParseFileAsync(string filePath, string vendorId, CancellationToken cancellationToken = default)
    {
        var vendor = _vendorSettings.Vendors.FirstOrDefault(v => v.Id == vendorId);
        if (vendor == null)
        {
            throw new ArgumentException($"Vendor {vendorId} not found");
        }

        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
        var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);

        // Decrypt if necessary
        if (vendor.FileSettings.IsEncrypted)
        {
            fileContent = await _encryptionService.DecryptAsync(fileContent);
        }

        List<FileRecord> records = fileExtension switch
        {
            ".csv" => ParseCsvFile(fileContent, vendor),
            ".json" => ParseJsonFile(fileContent, vendor),
            ".xml" => ParseXmlFile(fileContent, vendor),
            ".txt" => ParseTextFile(fileContent, vendor),
            _ => throw new NotSupportedException($"File format {fileExtension} is not supported")
        };

        // Validate records and log/report errors
        var errors = ValidateFileRecords(records);
        if (errors.Any())
        {
            foreach (var error in errors)
            {
                _logger.LogWarning(error);
            }
            // Optionally, throw or handle errors as needed
            // throw new InvalidDataException("Validation failed for one or more records.");
        }

        // Optionally, filter out invalid records
        // records = records.Where(r => !errors.Any(e => e.Contains(r.RecordId))).ToList();

        return records;
    }

    /// <summary>
    /// Example validation: checks for required fields, data types, and business rules.
    /// </summary>
    private List<string> ValidateFileRecords(List<FileRecord> records)
    {
        var errors = new List<string>();
        foreach (var record in records)
        {
            // Schema: Required field "Id"
            if (!record.Fields.TryGetValue("Id", out var id) || string.IsNullOrWhiteSpace(id))
                errors.Add($"Record {record.RecordId}: Missing or empty 'Id'.");

            // Data type: "Amount" should be a decimal
            if (record.Fields.TryGetValue("Amount", out var amountStr) && !decimal.TryParse(amountStr, out _))
                errors.Add($"Record {record.RecordId}: Invalid decimal in 'Amount'.");

            // Business rule: "Status" must be "Active" or "Inactive"
            if (record.Fields.TryGetValue("Status", out var status) &&
                !string.IsNullOrWhiteSpace(status) &&
                status != "Active" && status != "Inactive")
                errors.Add($"Record {record.RecordId}: Invalid 'Status' value '{status}'.");

            // Add more rules as needed...
        }
        return errors;
    }

    private List<FileRecord> ParseCsvFile(string content, VendorConfiguration vendor)
    {
        var records = new List<FileRecord>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
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
                RecordId = $"record_{i}",
                Fields = { fields }
            });
        }

        return records;
    }

    private List<FileRecord> ParseJsonFile(string content, VendorConfiguration vendor)
    {
        var records = new List<FileRecord>();
        using var document = JsonDocument.Parse(content);
        
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            int recordIndex = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var fields = new Dictionary<string, string>();
                foreach (var property in element.EnumerateObject())
                {
                    fields[property.Name] = property.Value.ToString();
                }

                records.Add(new FileRecord
                {
                    RecordId = $"record_{recordIndex++}",
                    Fields = { fields }
                });
            }
        }

        return records;
    }

    private List<FileRecord> ParseXmlFile(string content, VendorConfiguration vendor)
    {
        var records = new List<FileRecord>();
        var doc = new XmlDocument();
        doc.LoadXml(content);

        var recordNodes = doc.SelectNodes("//record") ?? doc.SelectNodes("//*[position()>1]");
        if (recordNodes == null) return records;

        int recordIndex = 0;
        foreach (XmlNode node in recordNodes)
        {
            var fields = new Dictionary<string, string>();
            
            if (node.Attributes != null)
            {
                foreach (XmlAttribute attr in node.Attributes)
                {
                    fields[attr.Name] = attr.Value;
                }
            }

            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element)
                {
                    fields[child.Name] = child.InnerText;
                }
            }

            records.Add(new FileRecord
            {
                RecordId = $"record_{recordIndex++}",
                Fields = { fields }
            });
        }

        return records;
    }

    private List<FileRecord> ParseTextFile(string content, VendorConfiguration vendor)
    {
        var records = new List<FileRecord>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int recordIndex = 0;
        foreach (var line in lines)
        {
            // Example: Assume each line is a key-value pair separated by ':' (customize as needed)
            var fields = new Dictionary<string, string>();
            var parts = line.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split(':', 2);
                if (kv.Length == 2)
                {
                    fields[kv[0].Trim()] = kv[1].Trim();
                }
            }

            if (fields.Count > 0)
            {
                records.Add(new FileRecord
                {
                    RecordId = $"record_{recordIndex++}",
                    Fields = { fields }
                });
            }
        }

        return records;
    }

    private async Task<ValidationResult> ValidateRecordsAsync(string vendorId, List<FileRecord> records, string correlationId, CancellationToken cancellationToken)
    {
        var vendor = _vendorSettings.Vendors.FirstOrDefault(v => v.Id == vendorId);
        if (vendor == null)
        {
            throw new ArgumentException($"Vendor {vendorId} not found");
        }

        using var channel = GrpcChannel.ForAddress(vendor.BusinessRulesServiceUrl);
        var client = new Contracts.BusinessRulesService.BusinessRulesServiceClient(channel);

        var request = new ValidateRecordsRequest
        {
            VendorId = vendorId,
            CorrelationId = correlationId
        };
        request.Records.AddRange(records);

        return await client.ValidateRecordsAsync(request, cancellationToken: cancellationToken);
    }

    private async Task ArchiveFileAsync(FileReceivedMessage message, ValidationResult validationResult, CancellationToken cancellationToken)
    {
        var archivePath = Path.Combine("archive", message.VendorId, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(archivePath);

        var archivedFilePath = Path.Combine(archivePath, $"{message.FileId}_{Path.GetFileName(message.FilePath)}");
        File.Copy(message.FilePath, archivedFilePath, true);

        // Create audit metadata
        var auditData = new
        {
            message.FileId,
            message.VendorId,
            message.FilePath,
            message.FileHash,
            message.FileSize,
            ProcessedAt = DateTimeOffset.UtcNow,
            ValidationResult = new
            {
                validationResult.IsValid,
                DiscrepancyCount = validationResult.Discrepancies.Count
            },
            ArchivedPath = archivedFilePath
        };

        var auditFilePath = Path.ChangeExtension(archivedFilePath, ".audit.json");
        await File.WriteAllTextAsync(auditFilePath, JsonSerializer.Serialize(auditData, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

        _logger.LogInformation("Archived file {FileId} to {ArchivedPath}", message.FileId, archivedFilePath);
    }
}
