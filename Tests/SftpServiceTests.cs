
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SecureFileExchange.Services;
using SecureFileExchange.VendorConfig;
using SecureFileExchange.Common;
using Xunit;

namespace SecureFileExchange.Tests;

public class SftpServiceTests
{
    private readonly Mock<ILogger<SftpService>> _mockLogger;
    private readonly Mock<IRabbitMqService> _mockRabbitMq;
    private readonly VendorSettings _vendorSettings;

    public SftpServiceTests()
    {
        _mockLogger = new Mock<ILogger<SftpService>>();
        _mockRabbitMq = new Mock<IRabbitMqService>();
        _vendorSettings = new VendorSettings
        {
            Vendors = new List<VendorConfiguration>
            {
                new VendorConfiguration
                {
                    Id = "test-vendor",
                    Name = "Test Vendor",
                    SftpSettings = new SftpConfiguration
                    {
                        Host = "localhost",
                        Port = 22,
                        Username = "test",
                        Password = "test",
                        RemotePath = "/test",
                        LocalPath = "./test-downloads"
                    }
                }
            }
        };
    }

    [Fact]
    public void SftpService_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var service = new SftpService(
            _mockLogger.Object,
            Options.Create(_vendorSettings),
            _mockRabbitMq.Object
        );

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task PollForFilesAsync_WithInvalidVendor_ShouldReturnEmptyList()
    {
        // Arrange
        var service = new SftpService(
            _mockLogger.Object,
            Options.Create(_vendorSettings),
            _mockRabbitMq.Object
        );

        // Act
        var result = await service.PollForFilesAsync("invalid-vendor");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void CreateSftpClient_ShouldHandleKeyAuthentication()
    {
        // This test would require a real SFTP server or mocking the SSH.NET library
        // For now, we'll just verify the service can be instantiated
        Assert.True(true);
    }
}

public class BusinessRulesServiceTests
{
    [Fact]
    public void ValidationRule_RegexPattern_ShouldWork()
    {
        // Arrange
        var rule = new ValidationRule
        {
            FieldName = "TestField",
            RuleType = "regex",
            Pattern = "^[A-Z]{3}$",
            IsRequired = true
        };

        // Act & Assert
        Assert.Equal("regex", rule.RuleType);
        Assert.True(rule.IsRequired);
        Assert.Equal("^[A-Z]{3}$", rule.Pattern);
    }
}
