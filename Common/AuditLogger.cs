
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SecureFileExchange.Common;

public interface IAuditLogger
{
    Task LogSecretAccessAsync(string secretName, string operation, string userId, bool success);
    Task LogCertificateOperationAsync(string certName, string operation, string userId, bool success);
    Task LogFileOperationAsync(string fileName, string operation, string vendorId, bool success);
}

public class BambooAuditLogger : IAuditLogger
{
    private readonly ILogger<BambooAuditLogger> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _auditServiceUrl;

    public BambooAuditLogger(ILogger<BambooAuditLogger> logger, IConfiguration configuration)
    {
        _logger = logger;
        _auditServiceUrl = configuration["BambooAudit:ServiceUrl"] ?? "https://audit.bamboo.internal";
        _httpClient = new HttpClient();
    }

    public async Task LogSecretAccessAsync(string secretName, string operation, string userId, bool success)
    {
        var auditEvent = new AuditEvent
        {
            EventType = "SECRET_ACCESS",
            ResourceName = secretName,
            Operation = operation,
            UserId = userId,
            Success = success,
            Timestamp = DateTime.UtcNow,
            ServiceName = "secure-file-exchange",
            Metadata = new Dictionary<string, object>
            {
                ["secret_type"] = GetSecretType(secretName),
                ["access_method"] = "bamboo_vault"
            }
        };

        await SendAuditEventAsync(auditEvent);
    }

    public async Task LogCertificateOperationAsync(string certName, string operation, string userId, bool success)
    {
        var auditEvent = new AuditEvent
        {
            EventType = "CERTIFICATE_OPERATION",
            ResourceName = certName,
            Operation = operation,
            UserId = userId,
            Success = success,
            Timestamp = DateTime.UtcNow,
            ServiceName = "secure-file-exchange",
            Metadata = new Dictionary<string, object>
            {
                ["cert_type"] = GetCertificateType(certName),
                ["ca"] = "bamboo_ca"
            }
        };

        await SendAuditEventAsync(auditEvent);
    }

    public async Task LogFileOperationAsync(string fileName, string operation, string vendorId, bool success)
    {
        var auditEvent = new AuditEvent
        {
            EventType = "FILE_OPERATION",
            ResourceName = fileName,
            Operation = operation,
            UserId = $"system.vendor.{vendorId}",
            Success = success,
            Timestamp = DateTime.UtcNow,
            ServiceName = "secure-file-exchange",
            Metadata = new Dictionary<string, object>
            {
                ["vendor_id"] = vendorId,
                ["file_size"] = GetFileSize(fileName)
            }
        };

        await SendAuditEventAsync(auditEvent);
    }

    private async Task SendAuditEventAsync(AuditEvent auditEvent)
    {
        try
        {
            var json = JsonSerializer.Serialize(auditEvent);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_auditServiceUrl}/v1/events", content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to send audit event to Bamboo Audit Service: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audit event to Bamboo Audit Service");
        }
        
        // Always log locally as backup
        _logger.LogInformation("AUDIT: {@AuditEvent}", auditEvent);
    }

    private string GetSecretType(string secretName)
    {
        if (secretName.Contains("ssh")) return "ssh_key";
        if (secretName.Contains("password")) return "password";
        if (secretName.Contains("api")) return "api_key";
        if (secretName.Contains("cert")) return "certificate";
        return "generic";
    }

    private string GetCertificateType(string certName)
    {
        if (certName.Contains("service")) return "service_cert";
        if (certName.Contains("client")) return "client_cert";
        if (certName.Contains("ca")) return "ca_cert";
        return "generic";
    }

    private long GetFileSize(string fileName)
    {
        try
        {
            return new FileInfo(fileName).Length;
        }
        catch
        {
            return 0;
        }
    }
}

public class AuditEvent
{
    public string EventType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
