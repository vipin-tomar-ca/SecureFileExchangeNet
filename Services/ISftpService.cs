
using SecureFileExchange.Contracts;

namespace SecureFileExchange.Services;

public interface ISftpService
{
    Task<List<FileReceivedMessage>> PollForFilesAsync(string vendorId, CancellationToken cancellationToken = default);
    Task<bool> DownloadFileAsync(string vendorId, string remoteFilePath, string localFilePath, CancellationToken cancellationToken = default);
    Task<string> CalculateFileHashAsync(string filePath);
}
