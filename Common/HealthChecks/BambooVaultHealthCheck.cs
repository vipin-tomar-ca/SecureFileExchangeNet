
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SecureFileExchange.Common.HealthChecks;

public class BambooVaultHealthCheck : IHealthCheck
{
    private readonly ISecretProvider _secretProvider;

    public BambooVaultHealthCheck(ISecretProvider secretProvider)
    {
        _secretProvider = secretProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isConnected = await _secretProvider.ValidateConnectivityAsync();
            
            if (isConnected)
            {
                return HealthCheckResult.Healthy("Bamboo Vault is accessible");
            }
            
            return HealthCheckResult.Unhealthy("Cannot connect to Bamboo Vault");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Bamboo Vault health check failed", ex);
        }
    }
}
