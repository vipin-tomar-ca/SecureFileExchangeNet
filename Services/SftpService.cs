
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

    public SftpService(
        ILogger<SftpService> logger,
        IOptions<VendorSettings> vendorSettings,
        IRabbitMqService rabbitMqService)
    {
        _logger = logger;
        _vendorSettings = vendorSettings.Value;
        _rabbitMqService = rabbitMqService;
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
            using var client = CreateSftpClient(vendor);
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

        using var client = CreateSftpClient(vendor);
        client.Connect();

        using var stream = new MemoryStream();
        client.DownloadFile(remoteFilePath, stream);
        
        client.Disconnect();
        
        return stream.ToArray();
    }

    private SftpClient CreateSftpClient(VendorConfiguration vendor)
    {
        var connectionInfo = new ConnectionInfo(
            vendor.SftpSettings.Host,
            vendor.SftpSettings.Port,
            vendor.SftpSettings.Username,
            CreateAuthenticationMethods(vendor.SftpSettings)
        );

        return new SftpClient(connectionInfo);
    }

    private AuthenticationMethod[] CreateAuthenticationMethods(SftpConfiguration sftpConfig)
    {
        var methods = new List<AuthenticationMethod>();

        if (!string.IsNullOrEmpty(sftpConfig.PrivateKeyPath))
        {
            var privateKeyFile = new PrivateKeyFile(sftpConfig.PrivateKeyPath, sftpConfig.PrivateKeyPassphrase);
            methods.Add(new PrivateKeyAuthenticationMethod(sftpConfig.Username, privateKeyFile));
        }

        if (!string.IsNullOrEmpty(sftpConfig.Password))
        {
            methods.Add(new PasswordAuthenticationMethod(sftpConfig.Username, sftpConfig.Password));
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
