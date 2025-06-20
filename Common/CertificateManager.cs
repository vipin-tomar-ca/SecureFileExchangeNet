
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace SecureFileExchange.Common;

public interface ICertificateManager
{
    Task<X509Certificate2> GetServiceCertificateAsync(string serviceName);
    Task<X509Certificate2> GetClientCertificateAsync(string clientName);
    Task<bool> ValidateCertificateAsync(X509Certificate2 certificate);
    Task RotateCertificateAsync(string certName);
    Task<TimeSpan> GetCertificateExpiryTimeAsync(string certName);
}

public class BambooCertificateManager : ICertificateManager
{
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<BambooCertificateManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly X509Certificate2 _caCertificate;

    public BambooCertificateManager(
        ISecretProvider secretProvider, 
        ILogger<BambooCertificateManager> logger,
        IConfiguration configuration)
    {
        _secretProvider = secretProvider;
        _logger = logger;
        _configuration = configuration;
        
        // Load Bamboo CA certificate
        var caPath = configuration["BambooCA:CertificatePath"] ?? "/etc/ssl/bamboo-ca.pem";
        _caCertificate = new X509Certificate2(caPath);
    }

    public async Task<X509Certificate2> GetServiceCertificateAsync(string serviceName)
    {
        try
        {
            _logger.LogDebug("Retrieving service certificate for {ServiceName}", serviceName);
            return await _secretProvider.GetCertificateAsync($"service-certs/{serviceName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve service certificate for {ServiceName}", serviceName);
            throw;
        }
    }

    public async Task<X509Certificate2> GetClientCertificateAsync(string clientName)
    {
        try
        {
            _logger.LogDebug("Retrieving client certificate for {ClientName}", clientName);
            return await _secretProvider.GetCertificateAsync($"client-certs/{clientName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve client certificate for {ClientName}", clientName);
            throw;
        }
    }

    public async Task<bool> ValidateCertificateAsync(X509Certificate2 certificate)
    {
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.ExtraStore.Add(_caCertificate);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            var isValid = chain.Build(certificate);
            
            if (!isValid)
            {
                foreach (var status in chain.ChainStatus)
                {
                    _logger.LogWarning("Certificate validation error: {Status}", status.StatusInformation);
                }
            }

            return isValid && certificate.NotAfter > DateTime.UtcNow.AddDays(30); // At least 30 days validity
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate validation failed");
            return false;
        }
    }

    public async Task RotateCertificateAsync(string certName)
    {
        try
        {
            _logger.LogInformation("Starting certificate rotation for {CertName}", certName);
            
            // Request new certificate from Bamboo CA
            var csrResult = await RequestNewCertificateAsync(certName);
            
            if (csrResult.Success)
            {
                await _secretProvider.RotateSecretAsync($"certificates/{certName}", csrResult.CertificateData);
                _logger.LogInformation("Successfully rotated certificate {CertName}", certName);
            }
            else
            {
                throw new InvalidOperationException($"Failed to obtain new certificate: {csrResult.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate rotation failed for {CertName}", certName);
            throw;
        }
    }

    public async Task<TimeSpan> GetCertificateExpiryTimeAsync(string certName)
    {
        try
        {
            var certificate = await _secretProvider.GetCertificateAsync(certName);
            return certificate.NotAfter - DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get certificate expiry for {CertName}", certName);
            throw;
        }
    }

    private async Task<CertificateRequestResult> RequestNewCertificateAsync(string certName)
    {
        // Integration with Bamboo Certificate Authority
        // This would call the internal CA API to generate a new certificate
        
        try
        {
            var caUrl = _configuration["BambooCA:ApiUrl"] ?? "https://ca.bamboo.internal";
            using var httpClient = new HttpClient();
            
            var requestPayload = new
            {
                common_name = certName,
                subject_alternative_names = new[] { $"{certName}.bamboo.internal" },
                key_type = "rsa",
                key_bits = 4096,
                ttl = "8760h" // 1 year
            };

            var response = await httpClient.PostAsJsonAsync($"{caUrl}/v1/pki/issue", requestPayload);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<dynamic>();
                return new CertificateRequestResult
                {
                    Success = true,
                    CertificateData = result?.certificate?.ToString() ?? string.Empty
                };
            }
            
            return new CertificateRequestResult
            {
                Success = false,
                Error = $"CA request failed with status {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new CertificateRequestResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

public class CertificateRequestResult
{
    public bool Success { get; set; }
    public string CertificateData { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
