
namespace SecureFileExchange.VendorConfig;

public class VendorSettings
{
    public string VendorId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SftpConfig Sftp { get; set; } = new();
    public EmailConfig Email { get; set; } = new();
    public ValidationRules Rules { get; set; } = new();
}

public class SftpConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string RemotePath { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 300;
}

public class EmailConfig
{
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string ImapHost { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public string ImapUsername { get; set; } = string.Empty;
    public string ImapPassword { get; set; } = string.Empty;
}

public class ValidationRules
{
    public List<FieldRule> FieldRules { get; set; } = new();
    public List<BusinessRule> BusinessRules { get; set; } = new();
}

public class FieldRule
{
    public string FieldName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? Pattern { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
}

public class BusinessRule
{
    public string RuleId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
