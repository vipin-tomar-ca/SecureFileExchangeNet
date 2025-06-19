
using MailKit.Net.Smtp;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;

namespace SecureFileExchange.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendDiscrepancyNotificationAsync(EmailDiscrepancyNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            var vendorConfig = GetVendorConfig(notification.VendorId);
            
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Secure File Exchange", vendorConfig.Email.FromAddress));
            message.To.Add(new MailboxAddress("Vendor Contact", vendorConfig.Email.ToAddress));
            message.Subject = $"File Validation Discrepancies - File ID: {notification.FileId}";

            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = CreateDiscrepancyEmailHtml(notification);
            bodyBuilder.TextBody = CreateDiscrepancyEmailText(notification);
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_configuration["Email:SmtpHost"], 
                int.Parse(_configuration["Email:SmtpPort"] ?? "587"), true, cancellationToken);
            
            await client.AuthenticateAsync(_configuration["Email:Username"], 
                _configuration["Email:Password"], cancellationToken);
            
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Discrepancy notification sent for file {FileId} to vendor {VendorId}", 
                notification.FileId, notification.VendorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending discrepancy notification for file {FileId}", notification.FileId);
        }
    }

    public async Task<List<ThirdPartyIssueReportedMessage>> PollEmailInboxAsync(string vendorId, CancellationToken cancellationToken = default)
    {
        var issues = new List<ThirdPartyIssueReportedMessage>();

        try
        {
            var vendorConfig = GetVendorConfig(vendorId);

            using var client = new ImapClient();
            await client.ConnectAsync(vendorConfig.Email.ImapHost, vendorConfig.Email.ImapPort, true, cancellationToken);
            await client.AuthenticateAsync(vendorConfig.Email.ImapUsername, vendorConfig.Email.ImapPassword, cancellationToken);

            var inbox = client.Inbox;
            await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly, cancellationToken);

            var searchResults = await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);

            foreach (var uid in searchResults)
            {
                var message = await inbox.GetMessageAsync(uid, cancellationToken);
                
                var issue = new ThirdPartyIssueReportedMessage
                {
                    VendorId = vendorId,
                    FileId = ExtractFileIdFromSubject(message.Subject),
                    IssueDescription = message.TextBody ?? message.HtmlBody ?? string.Empty,
                    EmailSubject = message.Subject,
                    ReceivedAt = DateTime.UtcNow.ToString("O"),
                    CorrelationId = Guid.NewGuid().ToString()
                };

                issues.Add(issue);
                _logger.LogInformation("Parsed issue report email for vendor {VendorId} with file ID {FileId}", 
                    vendorId, issue.FileId);
            }

            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling email inbox for vendor {VendorId}", vendorId);
        }

        return issues;
    }

    private string CreateDiscrepancyEmailHtml(EmailDiscrepancyNotification notification)
    {
        var html = $@"
        <html>
        <body>
            <h2>File Validation Discrepancies Report</h2>
            <p><strong>Vendor ID:</strong> {notification.VendorId}</p>
            <p><strong>File ID:</strong> {notification.FileId}</p>
            <p><strong>Correlation ID:</strong> {notification.CorrelationId}</p>
            
            <h3>Discrepancies Found:</h3>
            <table border='1' style='border-collapse: collapse;'>
                <tr>
                    <th>Record ID</th>
                    <th>Field Name</th>
                    <th>Expected</th>
                    <th>Actual</th>
                    <th>Rule Type</th>
                    <th>Description</th>
                </tr>";

        foreach (var discrepancy in notification.Discrepancies)
        {
            html += $@"
                <tr>
                    <td>{discrepancy.RecordId}</td>
                    <td>{discrepancy.FieldName}</td>
                    <td>{discrepancy.ExpectedValue}</td>
                    <td>{discrepancy.ActualValue}</td>
                    <td>{discrepancy.RuleType}</td>
                    <td>{discrepancy.Description}</td>
                </tr>";
        }

        html += @"
            </table>
            <p>Please review and resolve these discrepancies.</p>
        </body>
        </html>";

        return html;
    }

    private string CreateDiscrepancyEmailText(EmailDiscrepancyNotification notification)
    {
        var text = $@"
File Validation Discrepancies Report

Vendor ID: {notification.VendorId}
File ID: {notification.FileId}
Correlation ID: {notification.CorrelationId}

Discrepancies Found:
";

        foreach (var discrepancy in notification.Discrepancies)
        {
            text += $@"
- Record ID: {discrepancy.RecordId}
  Field: {discrepancy.FieldName}
  Expected: {discrepancy.ExpectedValue}
  Actual: {discrepancy.ActualValue}
  Rule Type: {discrepancy.RuleType}
  Description: {discrepancy.Description}
";
        }

        text += "\nPlease review and resolve these discrepancies.";
        return text;
    }

    private string ExtractFileIdFromSubject(string subject)
    {
        // Simple file ID extraction - would need more sophisticated parsing based on vendor email formats
        var match = System.Text.RegularExpressions.Regex.Match(subject, @"File ID:\s*([A-Za-z0-9\-]+)");
        return match.Success ? match.Groups[1].Value : "UNKNOWN";
    }

    private VendorSettings GetVendorConfig(string vendorId)
    {
        var vendorSection = _configuration.GetSection($"Vendors:{vendorId}");
        return vendorSection.Get<VendorSettings>() ?? throw new InvalidOperationException($"Vendor configuration not found for {vendorId}");
    }
}
