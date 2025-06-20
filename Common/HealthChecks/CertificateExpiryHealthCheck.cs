
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;

namespace SecureFileExchange.Common.HealthChecks;

public class CertificateExpiryHealthCheck : IHealthCheck
{
    private readonly ICertificateManager _certificateManager;
    private readonly IConfiguration _configuration;

    public CertificateExpiryHealthCheck(
        ICertificateManager certificateManager,
        IConfiguration configuration)
    {
        _certificateManager = certificateManager;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serviceName = _configuration["Service:Name"] ?? "unknown";
            var warningThresholdDays = _configuration.GetValue<int>("CertificateWarningDays", 30);
            
            var expiryTime = await _certificateManager.GetCertificateExpiryTimeAsync($"service-certs/{serviceName}");
            
            if (expiryTime.TotalDays < 0)
            {
                return HealthCheckResult.Unhealthy($"Service certificate has expired {Math.Abs(expiryTime.TotalDays):F0} days ago");
            }
            
            if (expiryTime.TotalDays < warningThresholdDays)
            {
                return HealthCheckResult.Degraded($"Service certificate expires in {expiryTime.TotalDays:F0} days");
            }
            
            return HealthCheckResult.Healthy($"Service certificate is valid for {expiryTime.TotalDays:F0} days");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Certificate expiry check failed", ex);
        }
    }
}
