using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureFileExchange.Services;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;
using SecureFileExchange.Common;
using System.Threading.Tasks;
using System.Collections.Generic;

public class FileProcessorServiceIntegrationTests
{
    [Fact]
    public async Task ProcessFileAsync_ProcessesAndArchivesFile()
    {
        // Arrange
        var logger = new LoggerFactory().CreateLogger<FileProcessorService>();
        var vendorSettings = new VendorSettings
        {
            Vendors = new List<VendorConfiguration>
            {
                new VendorConfiguration
                {
                    Id = "vendor1",
                    Name = "Vendor 1",
                    FileSettings = new FileConfiguration { IsEncrypted = false }, // Updated to use FileConfiguration
                    BusinessRulesServiceUrl = "http://localhost:5001" // Should point to a running test instance
                }
            }
        };  

        var rabbitMqService = new MockRabbitMqService();
        var encryptionService = new MockEncryptionService();
        var service = new FileProcessorService(
            logger,
            Options.Create(vendorSettings),
            rabbitMqService,
            encryptionService
        );

        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "Id,Amount\n1,100\n2,200");

        var message = new FileReceivedMessage
        {
            FileId = "testfile",
            VendorId = "vendor1",
            FilePath = path,
            FileHash = "dummyhash",
            FileSize = 100,
            ReceivedAt = System.DateTimeOffset.UtcNow.ToString("O"),
            CorrelationId = "corr-1"
        };

        // Act
        await service.ProcessFileAsync(message);

        // Assert: Check that archive file and audit file exist
        var archiveDir = Path.Combine("archive", "vendor1", System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
        var archivedFile = Directory.GetFiles(archiveDir, "*testfile*").FirstOrDefault();
        Assert.True(File.Exists(archivedFile));
        var auditFile = Path.ChangeExtension(archivedFile, ".audit.json");
        Assert.True(File.Exists(auditFile));

        // Cleanup
        File.Delete(path);
        File.Delete(archivedFile);
        File.Delete(auditFile);
    }
}

// Mock implementations for integration test
public class MockRabbitMqService : IRabbitMqService
{
    public Task PublishAsync<T>(string routingKey, T message, CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public void StartConsuming<T>(string queue, Func<T, Task> onMessage) where T : class { }

    public void StopConsuming() { }

    public Task<T?> ConsumeAsync<T>(string queueName, CancellationToken cancellationToken = default) where T : class
        => Task.FromResult<T?>(null);
}

public class MockEncryptionService : IEncryptionService
{
    public Task<string> EncryptAsync(string plainText) => Task.FromResult(plainText);
    public Task<string> DecryptAsync(string encryptedText) => Task.FromResult(encryptedText);
    public Task<byte[]> EncryptBytesAsync(byte[] data) => Task.FromResult(data);
    public Task<byte[]> DecryptBytesAsync(byte[] encryptedData) => Task.FromResult(encryptedData);
}