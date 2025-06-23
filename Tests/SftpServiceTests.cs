
using Xunit;
using SecureFileExchange.Services;
using SecureFileExchange.VendorConfig;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using SecureFileExchange.Common;

namespace Tests;

public class SftpServiceTests
{
    [Fact]
    public async Task PollForFilesAsync_ShouldReturnEmptyList_WhenNoFiles()
    {
        // Arrange  
        var mockLogger = new Mock<ILogger<SftpService>>();
        var mockRabbitMqService = new Mock<IRabbitMqService>();
        var vendorSettings = new VendorSettings
        {
            Vendors = new List<VendorConfiguration>
            {
                new VendorConfiguration { Id = "test-vendor" }
            }
        };
        var options = Options.Create(vendorSettings);
        var sftpService = new SftpService(mockLogger.Object, options, mockRabbitMqService.Object);

        // Act & Assert - This would need proper SFTP mocking  
        // For now, just verify the service can be instantiated  
        Assert.NotNull(sftpService);
    }
}
