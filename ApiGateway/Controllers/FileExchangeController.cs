
using Microsoft.AspNetCore.Mvc;
using SecureFileExchange.Services;

namespace SecureFileExchange.ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileExchangeController : ControllerBase
{
    private readonly ILogger<FileExchangeController> _logger;
    private readonly ISftpService _sftpService;
    private readonly IFileProcessorService _fileProcessorService;

    public FileExchangeController(ILogger<FileExchangeController> logger, 
                                 ISftpService sftpService,
                                 IFileProcessorService fileProcessorService)
    {
        _logger = logger;
        _sftpService = sftpService;
        _fileProcessorService = fileProcessorService;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }

    [HttpPost("trigger-sftp-poll/{vendorId}")]
    public async Task<IActionResult> TriggerSftpPoll(string vendorId)
    {
        try
        {
            var fileRec= await _sftpService.PollForFilesAsync(vendorId,new CancellationToken());
            return Ok(new { Message = $"SFTP poll triggered for vendor {vendorId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering SFTP poll for vendor {VendorId}", vendorId);
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }
}
