
using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecureFileExchange.Common;

namespace SecureFileExchange.Tools;

public class BambooVaultCli
{
    public static async Task<int> Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var serviceProvider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging(builder => builder.AddConsole())
            .AddBambooVault(configuration)
            .BuildServiceProvider();

        var rootCommand = new RootCommand("Bamboo Vault CLI for Secure File Exchange Platform");

        // Secret management commands
        var getSecretCommand = new Command("get-secret", "Retrieve a secret from Bamboo Vault");
        var secretNameOption = new Option<string>("--name", "Secret name") { IsRequired = true };
        getSecretCommand.AddOption(secretNameOption);
        getSecretCommand.SetHandler(async (string name) =>
        {
            var secretProvider = serviceProvider.GetRequiredService<ISecretProvider>();
            try
            {
                var secret = await secretProvider.GetSecretAsync(name);
                Console.WriteLine($"Secret {name}: {secret}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving secret: {ex.Message}");
            }
        }, secretNameOption);

        var rotateSecretCommand = new Command("rotate-secret", "Rotate a secret in Bamboo Vault");
        var rotateNameOption = new Option<string>("--name", "Secret name") { IsRequired = true };
        var newValueOption = new Option<string>("--value", "New secret value") { IsRequired = true };
        rotateSecretCommand.AddOption(rotateNameOption);
        rotateSecretCommand.AddOption(newValueOption);
        rotateSecretCommand.SetHandler(async (string name, string value) =>
        {
            var secretProvider = serviceProvider.GetRequiredService<ISecretProvider>();
            try
            {
                await secretProvider.RotateSecretAsync(name, value);
                Console.WriteLine($"Successfully rotated secret {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rotating secret: {ex.Message}");
            }
        }, rotateNameOption, newValueOption);

        // Certificate management commands
        var rotateCertCommand = new Command("rotate-cert", "Rotate a certificate");
        var certNameOption = new Option<string>("--name", "Certificate name") { IsRequired = true };
        rotateCertCommand.AddOption(certNameOption);
        rotateCertCommand.SetHandler(async (string name) =>
        {
            var certManager = serviceProvider.GetRequiredService<ICertificateManager>();
            try
            {
                await certManager.RotateCertificateAsync(name);
                Console.WriteLine($"Successfully rotated certificate {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rotating certificate: {ex.Message}");
            }
        }, certNameOption);

        var checkCertCommand = new Command("check-cert", "Check certificate expiry");
        var checkCertNameOption = new Option<string>("--name", "Certificate name") { IsRequired = true };
        checkCertCommand.AddOption(checkCertNameOption);
        checkCertCommand.SetHandler(async (string name) =>
        {
            var certManager = serviceProvider.GetRequiredService<ICertificateManager>();
            try
            {
                var expiry = await certManager.GetCertificateExpiryTimeAsync(name);
                Console.WriteLine($"Certificate {name} expires in {expiry.TotalDays:F0} days");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking certificate: {ex.Message}");
            }
        }, checkCertNameOption);

        // Health check command
        var healthCheckCommand = new Command("health", "Check Bamboo Vault connectivity");
        healthCheckCommand.SetHandler(async () =>
        {
            var secretProvider = serviceProvider.GetRequiredService<ISecretProvider>();
            try
            {
                var isHealthy = await secretProvider.ValidateConnectivityAsync();
                Console.WriteLine($"Bamboo Vault health: {(isHealthy ? "OK" : "FAILED")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Health check failed: {ex.Message}");
            }
        });

        rootCommand.Add(getSecretCommand);
        rootCommand.Add(rotateSecretCommand);
        rootCommand.Add(rotateCertCommand);
        rootCommand.Add(checkCertCommand);
        rootCommand.Add(healthCheckCommand);

        return await rootCommand.InvokeAsync(args);
    }
}
