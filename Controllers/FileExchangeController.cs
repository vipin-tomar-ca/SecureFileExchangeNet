
using Microsoft.AspNetCore.Mvc;
using SecureFileExchange.Services;

namespace SecureFileExchange.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileExchangeController : ControllerBase
{
    private readonly ILogger<FileExchangeController> _logger;
    private readonly ISftpService _sftpService;
    private readonly IFileProcessorService _fileProcessorService;
    private readonly IEmailService _emailService;

    public FileExchangeController(
        ILogger<FileExchangeController> logger,
        ISftpService sftpService,
        IFileProcessorService fileProcessorService,
        IEmailService emailService)
    {
        _logger = logger;
        _sftpService = sftpService;
        _fileProcessorService = fileProcessorService;
        _emailService = emailService;
    }

    [HttpPost("poll-sftp/{vendorId}")]
    public async Task<IActionResult> PollSftp(string vendorId, CancellationToken cancellationToken)
    {
        try
        {
            var files = await _sftpService.PollForFilesAsync(vendorId, cancellationToken);
            
            foreach (var file in files)
            {
                await _fileProcessorService.ProcessFileAsync(file, cancellationToken);
            }

            return Ok(new { ProcessedFiles = files.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling SFTP for vendor {VendorId}", vendorId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("poll-email/{vendorId}")]
    public async Task<IActionResult> PollEmail(string vendorId, CancellationToken cancellationToken)
    {
        try
        {
            var issues = await _emailService.PollEmailInboxAsync(vendorId, cancellationToken);
            return Ok(new { IssuesFound = issues.Count, Issues = issues });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling email for vendor {VendorId}", vendorId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}
