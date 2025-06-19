
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;

namespace SecureFileExchange.Services;

public class SftpService : ISftpService
{
    private readonly ILogger<SftpService> _logger;
    private readonly IConfiguration _configuration;

    public SftpService(ILogger<SftpService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<List<FileReceivedMessage>> PollForFilesAsync(string vendorId, CancellationToken cancellationToken = default)
    {
        var vendorConfig = GetVendorConfig(vendorId);
        var files = new List<FileReceivedMessage>();

        try
        {
            using var privateKey = new PrivateKeyFile(vendorConfig.Sftp.PrivateKeyPath);
            var connectionInfo = new ConnectionInfo(vendorConfig.Sftp.Host, vendorConfig.Sftp.Port, vendorConfig.Sftp.Username, privateKey);

            using var sftpClient = new SftpClient(connectionInfo);
            await Task.Run(() => sftpClient.Connect(), cancellationToken);

            var remoteFiles = sftpClient.ListDirectory(vendorConfig.Sftp.RemotePath)
                .Where(f => f.IsRegularFile && !f.Name.StartsWith('.'))
                .ToList();

            foreach (var remoteFile in remoteFiles)
            {
                var localFilePath = Path.Combine(vendorConfig.Sftp.LocalPath, remoteFile.Name);
                
                if (await DownloadFileAsync(vendorId, remoteFile.FullName, localFilePath, cancellationToken))
                {
                    var fileHash = await CalculateFileHashAsync(localFilePath);
                    var correlationId = Guid.NewGuid().ToString();

                    files.Add(new FileReceivedMessage
                    {
                        FileId = Guid.NewGuid().ToString(),
                        VendorId = vendorId,
                        FilePath = localFilePath,
                        FileHash = fileHash,
                        FileSize = remoteFile.Length,
                        ReceivedAt = DateTime.UtcNow.ToString("O"),
                        CorrelationId = correlationId
                    });

                    _logger.LogInformation("Downloaded file {FileName} from vendor {VendorId} with correlation ID {CorrelationId}", 
                        remoteFile.Name, vendorId, correlationId);
                }
            }

            sftpClient.Disconnect();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling SFTP for vendor {VendorId}", vendorId);
        }

        return files;
    }

    public async Task<bool> DownloadFileAsync(string vendorId, string remoteFilePath, string localFilePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var vendorConfig = GetVendorConfig(vendorId);
            
            Directory.CreateDirectory(Path.GetDirectoryName(localFilePath)!);

            using var privateKey = new PrivateKeyFile(vendorConfig.Sftp.PrivateKeyPath);
            var connectionInfo = new ConnectionInfo(vendorConfig.Sftp.Host, vendorConfig.Sftp.Port, vendorConfig.Sftp.Username, privateKey);

            using var sftpClient = new SftpClient(connectionInfo);
            await Task.Run(() => sftpClient.Connect(), cancellationToken);

            using var fileStream = File.OpenWrite(localFilePath);
            await Task.Run(() => sftpClient.DownloadFile(remoteFilePath, fileStream), cancellationToken);

            sftpClient.Disconnect();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {RemoteFilePath} for vendor {VendorId}", remoteFilePath, vendorId);
            return false;
        }
    }

    public async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var fileStream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(fileStream);
        return Convert.ToHexString(hashBytes);
    }

    private VendorSettings GetVendorConfig(string vendorId)
    {
        var vendorSection = _configuration.GetSection($"Vendors:{vendorId}");
        return vendorSection.Get<VendorSettings>() ?? throw new InvalidOperationException($"Vendor configuration not found for {vendorId}");
    }
}
