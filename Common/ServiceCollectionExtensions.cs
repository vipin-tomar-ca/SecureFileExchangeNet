
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SecureFileExchange.Common.HealthChecks;

namespace SecureFileExchange.Common;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBambooVault(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISecretProvider, BambooVaultClient>();
        services.AddSingleton<ICertificateManager, BambooCertificateManager>();
        services.AddSingleton<IKeyManagementService, BambooKmsService>();
        
        return services;
    }

    public static IServiceCollection AddBambooHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<BambooVaultHealthCheck>("bamboo-vault")
            .AddCheck<CertificateExpiryHealthCheck>("certificate-expiry");
        
        return services;
    }

    public static IServiceCollection AddMutualTlsAuthentication(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure HttpClient with mutual TLS for service-to-service communication
        services.AddHttpClient("mutual-tls", client =>
        {
            // Configure with certificates from Bamboo Vault
        }).ConfigurePrimaryHttpMessageHandler(provider =>
        {
            var certManager = provider.GetRequiredService<ICertificateManager>();
            var serviceName = configuration["Service:Name"] ?? "unknown";
            
            var handler = new HttpClientHandler();
            
            // This would be configured with the actual certificate
            // handler.ClientCertificates.Add(await certManager.GetServiceCertificateAsync(serviceName));
            
            return handler;
        });

        return services;
    }
}
