
using SecureFileExchange.Contracts;

namespace SecureFileExchange.Services;

public interface IFileProcessorService
{
    Task ProcessFileAsync(FileReceivedMessage message, CancellationToken cancellationToken = default);
    Task<List<FileRecord>> ParseFileAsync(string filePath, string vendorId, CancellationToken cancellationToken = default);
}
using SecureFileExchange.Contracts;

namespace SecureFileExchange.Services;

public interface IFileProcessorService
{
    Task ProcessFileAsync(FileReceivedMessage message, CancellationToken cancellationToken = default);
    Task<List<FileRecord>> ParseFileAsync(string filePath, string vendorId, CancellationToken cancellationToken = default);
}
