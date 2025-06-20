
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace SecureFileExchange.Common;

public interface IKeyManagementService
{
    Task<byte[]> EncryptAsync(byte[] plaintext, string keyId);
    Task<byte[]> DecryptAsync(byte[] ciphertext, string keyId);
    Task<string> GenerateDataKeyAsync(string keyId);
    Task<byte[]> DeriveKeyAsync(string keyId, string context);
    Task RotateKeyAsync(string keyId);
}

public class BambooKmsService : IKeyManagementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BambooKmsService> _logger;
    private readonly string _kmsBaseUrl;
    private readonly ISecretProvider _secretProvider;

    public BambooKmsService(
        IConfiguration configuration, 
        ILogger<BambooKmsService> logger,
        ISecretProvider secretProvider)
    {
        _logger = logger;
        _secretProvider = secretProvider;
        _kmsBaseUrl = configuration["BambooKMS:BaseUrl"] ?? "https://kms.bamboo.internal";
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GetKmsToken()}");
    }

    public async Task<byte[]> EncryptAsync(byte[] plaintext, string keyId)
    {
        try
        {
            _logger.LogDebug("Encrypting data with key {KeyId}", keyId);
            
            var payload = new
            {
                key_id = keyId,
                plaintext = Convert.ToBase64String(plaintext),
                context = new { service = "secure-file-exchange" }
            };

            var response = await _httpClient.PostAsJsonAsync($"{_kmsBaseUrl}/v1/encrypt", payload);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<KmsResponse>();
            return Convert.FromBase64String(result?.Ciphertext ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption failed for key {KeyId}", keyId);
            throw;
        }
    }

    public async Task<byte[]> DecryptAsync(byte[] ciphertext, string keyId)
    {
        try
        {
            _logger.LogDebug("Decrypting data with key {KeyId}", keyId);
            
            var payload = new
            {
                key_id = keyId,
                ciphertext = Convert.ToBase64String(ciphertext),
                context = new { service = "secure-file-exchange" }
            };

            var response = await _httpClient.PostAsJsonAsync($"{_kmsBaseUrl}/v1/decrypt", payload);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<KmsResponse>();
            return Convert.FromBase64String(result?.Plaintext ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed for key {KeyId}", keyId);
            throw;
        }
    }

    public async Task<string> GenerateDataKeyAsync(string keyId)
    {
        try
        {
            var payload = new
            {
                key_id = keyId,
                key_spec = "AES_256"
            };

            var response = await _httpClient.PostAsJsonAsync($"{_kmsBaseUrl}/v1/generate-data-key", payload);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<KmsDataKeyResponse>();
            return result?.PlaintextKey ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data key generation failed for key {KeyId}", keyId);
            throw;
        }
    }

    public async Task<byte[]> DeriveKeyAsync(string keyId, string context)
    {
        try
        {
            var payload = new
            {
                key_id = keyId,
                context = context,
                key_length = 32 // 256 bits
            };

            var response = await _httpClient.PostAsJsonAsync($"{_kmsBaseUrl}/v1/derive-key", payload);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<KmsResponse>();
            return Convert.FromBase64String(result?.DerivedKey ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Key derivation failed for key {KeyId}", keyId);
            throw;
        }
    }

    public async Task RotateKeyAsync(string keyId)
    {
        try
        {
            _logger.LogInformation("Rotating KMS key {KeyId}", keyId);
            
            var response = await _httpClient.PostAsync($"{_kmsBaseUrl}/v1/keys/{keyId}/rotate", null);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Successfully rotated KMS key {KeyId}", keyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Key rotation failed for key {KeyId}", keyId);
            throw;
        }
    }

    private string GetKmsToken()
    {
        return Environment.GetEnvironmentVariable("KMS_TOKEN") ?? 
               throw new InvalidOperationException("KMS_TOKEN environment variable not set");
    }
}

public class KmsResponse
{
    public string? Ciphertext { get; set; }
    public string? Plaintext { get; set; }
    public string? DerivedKey { get; set; }
    public string? KeyId { get; set; }
}

public class KmsDataKeyResponse
{
    public string? PlaintextKey { get; set; }
    public string? EncryptedKey { get; set; }
    public string? KeyId { get; set; }
}
