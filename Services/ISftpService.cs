
using SecureFileExchange.Contracts;

namespace SecureFileExchange.Services;

public interface ISftpService
{
    Task<List<FileReceivedMessage>> PollForFilesAsync(string vendorId, CancellationToken cancellationToken = default);
    //Task<bool> DownloadFileAsync(string vendorId, string remoteFilePath, string localFilePath, CancellationToken cancellationToken = default);
    //Task<string> CalculateFileHashAsync(string filePath);

    //Task<List<FileInfo>> PollForFilesAsync(string vendorId, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadFileAsync(string vendorId, string filePath, CancellationToken cancellationToken = default);

}

public class FileInfo
{
    public string Path { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public long Size { get; set; }
}
