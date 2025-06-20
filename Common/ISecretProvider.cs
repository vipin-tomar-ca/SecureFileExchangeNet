
using System.Security.Cryptography.X509Certificates;

namespace SecureFileExchange.Common;

public interface ISecretProvider
{
    Task<string> GetSecretAsync(string secretName);
    Task<T> GetSecretAsync<T>(string secretName) where T : class;
    Task<byte[]> GetBinarySecretAsync(string secretName);
    Task<X509Certificate2> GetCertificateAsync(string certName);
    Task RotateSecretAsync(string secretName, string newValue);
    Task<bool> ValidateConnectivityAsync();
}

public class SecretMetadata
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastRotated { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Version { get; set; } = string.Empty;
}
