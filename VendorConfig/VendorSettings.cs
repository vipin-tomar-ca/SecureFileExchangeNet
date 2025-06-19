namespace SecureFileExchange.VendorConfig;

public class VendorSettings
{
    public List<VendorConfiguration> Vendors { get; set; } = new();
}

public class VendorConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SftpConfiguration Sftp { get; set; } = new();
    public EmailConfiguration Email { get; set; } = new();
    public List<ValidationRule> ValidationRules { get; set; } = new();
}

public class SftpConfiguration
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
    public string RemotePath { get; set; } = string.Empty;
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
    public string ToAddress { get; set; } = string.Empty;
}

public class ValidationRule
{
    public string FieldName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}