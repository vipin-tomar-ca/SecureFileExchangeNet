
namespace SecureFileExchange.VendorConfig;

public class VendorSettings
{
    public List<VendorConfiguration> Vendors { get; set; } = new();
}

public class VendorConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BusinessRulesServiceUrl { get; set; } = "https://localhost:5001";
    public SftpConfiguration SftpSettings { get; set; } = new();
    public EmailConfiguration EmailSettings { get; set; } = new();
    public FileConfiguration FileSettings { get; set; } = new();
    public List<ValidationRule> ValidationRules { get; set; } = new();
}

public class SftpConfiguration
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string PrivateKeyPassphrase { get; set; } = string.Empty;
    public string RemotePath { get; set; } = "/";
    public string LocalPath { get; set; } = "./downloads";
    public int PollIntervalSeconds { get; set; } = 300; // 5 minutes
}

public class EmailConfiguration
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string ImapHost { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public bool UseStartTls { get; set; } = true;
    public string VendorDomain { get; set; } = string.Empty;
    public List<string> NotificationRecipients { get; set; } = new();
    public List<string> AdminRecipients { get; set; } = new();
}

public class FileConfiguration
{
    public string FileFormat { get; set; } = "csv"; // csv, json, xml
    public bool IsEncrypted { get; set; } = false;
    public string EncryptionKey { get; set; } = string.Empty;
    public bool HasHeader { get; set; } = true;
    public string Delimiter { get; set; } = ",";
    public string DateFormat { get; set; } = "yyyy-MM-dd";
}

public class ValidationRule
{
    public string FieldName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty; // regex, range, length, exactvalue, date
    public bool IsRequired { get; set; } = false;
    public string? Pattern { get; set; }
    public string? ExpectedValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? ErrorMessage { get; set; }
    public string Severity { get; set; } = "Error"; // Error, Warning
}
