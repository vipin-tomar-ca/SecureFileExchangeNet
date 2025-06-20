
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SecureFileExchange.Common;

public class BambooVaultClient : ISecretProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BambooVaultClient> _logger;
    private readonly string _vaultBaseUrl;
    private readonly string _namespace;
    private readonly X509Certificate2 _clientCertificate;

    public BambooVaultClient(IConfiguration configuration, ILogger<BambooVaultClient> logger)
    {
        _logger = logger;
        _vaultBaseUrl = configuration["BambooVault:BaseUrl"] ?? "https://vault.bamboo.internal";
        _namespace = configuration["BambooVault:Namespace"] ?? "secure-file-exchange";
        
        var certPath = configuration["BambooVault:ClientCertPath"] ?? "client-cert.pfx";
        var certPassword = Environment.GetEnvironmentVariable("VAULT_CLIENT_SECRET") ?? string.Empty;
        
        _clientCertificate = new X509Certificate2(certPath, certPassword);
        
        _httpClient = new HttpClient(new HttpClientHandler()
        {
            ClientCertificates = { _clientCertificate }
        });
        
        _httpClient.DefaultRequestHeaders.Add("X-Vault-Namespace", _namespace);
        _httpClient.DefaultRequestHeaders.Add("X-Vault-Token", GetVaultToken());
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        try
        {
            _logger.LogDebug("Retrieving secret {SecretName} from Bamboo Vault", secretName);
            
            var response = await _httpClient.GetAsync($"{_vaultBaseUrl}/v1/secrets/data/{secretName}");
            response.EnsureSuccessStatusCode();
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            var vaultResponse = JsonSerializer.Deserialize<VaultResponse>(jsonContent);
            
            if (vaultResponse?.Data?.Data?.ContainsKey("value") == true)
            {
                return DecryptSecret(vaultResponse.Data.Data["value"].ToString() ?? string.Empty);
            }
            
            throw new InvalidOperationException($"Secret {secretName} not found in vault");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret {SecretName}", secretName);
            throw;
        }
    }

    public async Task<T> GetSecretAsync<T>(string secretName) where T : class
    {
        var secretJson = await GetSecretAsync(secretName);
        return JsonSerializer.Deserialize<T>(secretJson) ?? throw new InvalidOperationException($"Cannot deserialize secret {secretName}");
    }

    public async Task<byte[]> GetBinarySecretAsync(string secretName)
    {
        var secretBase64 = await GetSecretAsync(secretName);
        return Convert.FromBase64String(secretBase64);
    }

    public async Task<X509Certificate2> GetCertificateAsync(string certName)
    {
        try
        {
            var certData = await GetBinarySecretAsync($"certificates/{certName}");
            var password = await GetSecretAsync($"certificates/{certName}-password");
            
            return new X509Certificate2(certData, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve certificate {CertName}", certName);
            throw;
        }
    }

    public async Task RotateSecretAsync(string secretName, string newValue)
    {
        try
        {
            _logger.LogInformation("Rotating secret {SecretName}", secretName);
            
            var encryptedValue = EncryptSecret(newValue);
            var payload = new
            {
                data = new Dictionary<string, object>
                {
                    ["value"] = encryptedValue,
                    ["rotated_at"] = DateTime.UtcNow.ToString("O"),
                    ["rotated_by"] = Environment.UserName
                }
            };
            
            var jsonContent = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_vaultBaseUrl}/v1/secrets/data/{secretName}", content);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Successfully rotated secret {SecretName}", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate secret {SecretName}", secretName);
            throw;
        }
    }

    public async Task<bool> ValidateConnectivityAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_vaultBaseUrl}/v1/sys/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string GetVaultToken()
    {
        return Environment.GetEnvironmentVariable("VAULT_TOKEN") ?? 
               throw new InvalidOperationException("VAULT_TOKEN environment variable not set");
    }

    private string DecryptSecret(string encryptedValue)
    {
        // Implementation would use Bamboo KMS for decryption
        // For demo purposes, using base64 decode
        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encryptedValue));
        }
        catch
        {
            return encryptedValue; // Fallback for unencrypted values
        }
    }

    private string EncryptSecret(string plainValue)
    {
        // Implementation would use Bamboo KMS for encryption
        // For demo purposes, using base64 encode
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainValue));
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _clientCertificate?.Dispose();
    }
}

public class VaultResponse
{
    public VaultData? Data { get; set; }
}

public class VaultData
{
    public Dictionary<string, object>? Data { get; set; }
    public VaultMetadata? Metadata { get; set; }
}

public class VaultMetadata
{
    public DateTime Created_time { get; set; }
    public DateTime Deletion_time { get; set; }
    public bool Destroyed { get; set; }
    public int Version { get; set; }
}
