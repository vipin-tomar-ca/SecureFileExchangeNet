using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;
using SecureFileExchange.Common;

namespace SecureFileExchange.Services;

public interface ISftpService
{
    Task<List<FileReceivedMessage>> PollForFilesAsync(string vendorId, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadFileAsync(string vendorId, string remoteFilePath, CancellationToken cancellationToken = default);
}

public class SftpService : ISftpService
{
    private readonly ILogger<SftpService> _logger;
    private readonly VendorSettings _vendorSettings;
    private readonly IRabbitMqService _rabbitMqService;
    private readonly ISecretProvider _secretProvider;

    public SftpService(
        ILogger<SftpService> logger,
        IOptions<VendorSettings> vendorSettings,
        IRabbitMqService rabbitMqService,
        ISecretProvider secretProvider)
    {
        _logger = logger;
        _vendorSettings = vendorSettings.Value;
        _rabbitMqService = rabbitMqService;
        _secretProvider = secretProvider;
    }

    public async Task<List<FileReceivedMessage>> PollForFilesAsync(string vendorId, CancellationToken cancellationToken = default)
    {
        var vendor = _vendorSettings.Vendors.FirstOrDefault(v => v.Id == vendorId);
        if (vendor == null)
        {
            _logger.LogWarning("Vendor {VendorId} not found", vendorId);
            return new List<FileReceivedMessage>();
        }

        var files = new List<FileReceivedMessage>();

        try
        {
            var sftpConfig = vendor.SftpSettings;
            var authMethods = await GetAuthenticationMethodsAsync(sftpConfig, vendorId);

            var connectionInfo = new ConnectionInfo(
                vendor.SftpSettings.Host,
                vendor.SftpSettings.Port,
                vendor.SftpSettings.Username,
                authMethods
            );
            using var client = new SftpClient(connectionInfo);

            client.Connect();

            var remoteFiles = client.ListDirectory(vendor.SftpSettings.RemotePath)
                .Where(f => f.IsRegularFile && !f.Name.StartsWith("."))
                .ToList();

            foreach (var file in remoteFiles)
            {
                try
                {
                    var fileData = await DownloadFileAsync(vendorId, file.FullName, cancellationToken);
                    var fileHash = ComputeFileHash(fileData);

                    var localPath = Path.Combine(vendor.SftpSettings.LocalPath, file.Name);
                    await File.WriteAllBytesAsync(localPath, fileData, cancellationToken);

                    var message = new FileReceivedMessage
                    {
                        FileId = Guid.NewGuid().ToString(),
                        VendorId = vendorId,
                        FilePath = localPath,
                        FileHash = fileHash,
                        FileSize = fileData.Length,
                        ReceivedAt = DateTimeOffset.UtcNow.ToString("O"),
                        CorrelationId = Guid.NewGuid().ToString()
                    };

                    files.Add(message);
                    _logger.LogInformation("Downloaded file {FileName} from vendor {VendorId}", file.Name, vendorId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download file {FileName} from vendor {VendorId}", file.Name, vendorId);
                }
            }

            client.Disconnect();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SFTP server for vendor {VendorId}", vendorId);
        }

        return files;
    }

    public async Task<byte[]> DownloadFileAsync(string vendorId, string remoteFilePath, CancellationToken cancellationToken = default)
    {
        var vendor = _vendorSettings.Vendors.FirstOrDefault(v => v.Id == vendorId);
        if (vendor == null)
        {
            throw new ArgumentException($"Vendor {vendorId} not found");
        }

        var sftpConfig = vendor.SftpSettings;
        var authMethods = await GetAuthenticationMethodsAsync(sftpConfig, vendorId);

        var connectionInfo = new ConnectionInfo(
            vendor.SftpSettings.Host,
            vendor.SftpSettings.Port,
            vendor.SftpSettings.Username,
            authMethods
        );

        using var client = new SftpClient(connectionInfo);
        client.Connect();

        using var stream = new MemoryStream();
        client.DownloadFile(remoteFilePath, stream);

        client.Disconnect();

        return stream.ToArray();
    }


    private async Task<AuthenticationMethod[]> GetAuthenticationMethodsAsync(SftpConfiguration sftpConfig, string vendorId)
    {
        var methods = new List<AuthenticationMethod>();

        try
        {
            // Get SSH private key from Bamboo Vault
            // Assuming _secretProvider.GetBinarySecretAsync returns the private key data
            // and _secretProvider.GetSecretAsync returns the passphrase.
            var privateKeyData = await _secretProvider.GetBinarySecretAsync($"sftp/{vendorId}/ssh-private-key");
            var privateKeyPassphrase = await _secretProvider.GetSecretAsync($"sftp/{vendorId}/ssh-passphrase");

            using var keyStream = new MemoryStream(privateKeyData);
            var privateKeyFile = new PrivateKeyFile(keyStream, privateKeyPassphrase);
            methods.Add(new PrivateKeyAuthenticationMethod(sftpConfig.Username, privateKeyFile));

            _logger.LogDebug("Using SSH key authentication for vendor {VendorId}", vendorId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSH key authentication not available for vendor {VendorId}, trying password", vendorId);

            try
            {
                // Fallback to password authentication from Bamboo Vault
                var password = await _secretProvider.GetSecretAsync($"sftp/{vendorId}/password");
                methods.Add(new PasswordAuthenticationMethod(sftpConfig.Username, password));

                _logger.LogDebug("Using password authentication for vendor {VendorId}", vendorId);
            }
            catch (Exception passwordEx)
            {
                _logger.LogError(passwordEx, "No valid authentication method found for vendor {VendorId}", vendorId);
                throw new InvalidOperationException($"No authentication credentials found in Bamboo Vault for vendor {vendorId}");
            }
        }

        return methods.ToArray();
    }


    private string ComputeFileHash(byte[] fileData)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(fileData);
        return Convert.ToHexString(hashBytes);
    }
}