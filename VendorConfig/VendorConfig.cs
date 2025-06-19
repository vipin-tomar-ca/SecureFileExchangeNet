
namespace SecureFileExchange.VendorConfig;

public class VendorConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SftpConfig Sftp { get; set; } = new();
    public EmailConfig Email { get; set; } = new();
    public List<ValidationRule> ValidationRules { get; set; } = new();
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
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string ImapHost { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string ImapUsername { get; set; } = string.Empty;
    public string ImapPassword { get; set; } = string.Empty;
}
