using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureFileExchange.Services;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;
using SecureFileExchange.Common;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

public class FileProcessorServiceTests
{
    private readonly Mock<ILogger<FileProcessorService>> _logger = new();
    private readonly Mock<IRabbitMqService> _rabbitMqService = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();
    // Update the VendorSettings initialization to use the correct type
    private readonly VendorSettings _vendorSettings = new()
    {
        Vendors = new List<VendorConfiguration>
        {
            new VendorConfiguration
            {
                Id = "vendor1",
                Name = "Vendor 1",
                FileSettings = new FileConfiguration { IsEncrypted = false } // Replace 'FileSettings' with 'FileConfiguration'
            }
        }
    };

    private FileProcessorService CreateService()
    {
        return new FileProcessorService(
            _logger.Object,
            Options.Create(_vendorSettings),
            _rabbitMqService.Object,
            _encryptionService.Object
        );
    }

    [Fact]
    public async Task ParseFileAsync_ThrowsForUnknownVendor()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ParseFileAsync("file.csv", "unknown", CancellationToken.None));
    }

    [Fact]
    public async Task ParseFileAsync_ParsesCsvFile()
    {
        var service = CreateService();
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "Id,Amount\n1,100\n2,200");
        var records = await service.ParseFileAsync(path, "vendor1");
        Assert.Equal(2, records.Count);
        Assert.Equal("1", records[0].Fields["Id"]);
        Assert.Equal("200", records[1].Fields["Amount"]);
        File.Delete(path);
    }

    [Fact]
    public async Task ParseFileAsync_ParsesTextFile()
    {
        var service = CreateService();
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "Id:1;Amount:100\nId:2;Amount:200");
        var records = await service.ParseFileAsync(path, "vendor1");
        Assert.Equal(2, records.Count);
        Assert.Equal("1", records[0].Fields["Id"]);
        Assert.Equal("200", records[1].Fields["Amount"]);
        File.Delete(path);
    }
}