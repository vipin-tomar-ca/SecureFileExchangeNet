
using MailKit.Net.Smtp;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using SecureFileExchange.Contracts;
using SecureFileExchange.VendorConfig;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text;

namespace SecureFileExchange.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly VendorSettings _vendorSettings;

    public EmailService(
        ILogger<EmailService> logger,
        IOptions<VendorSettings> vendorSettings)
    {
        _logger = logger;
        _vendorSettings = vendorSettings.Value;
    }

    public async Task SendDiscrepancyNotificationAsync(EmailDiscrepancyNotification notification, CancellationToken cancellationToken = default)
    {
        var vendor = _vendorSettings.Vendors.FirstOrDefault(v => v.Id == notification.VendorId);
        if (vendor == null)
        {
            _logger.LogWarning("Vendor {VendorId} not found for email notification", notification.VendorId);
            return;
        }

        try
        {
            var message = CreateDiscrepancyEmail(notification, vendor);
            await SendEmailAsync(message, cancellationToken);
            
            _logger.LogInformation("Sent discrepancy notification for file {FileId} to {Recipients}", 
                notification.FileId, string.Join(", ", vendor.EmailSettings.NotificationRecipients));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send discrepancy notification for file {FileId}", notification.FileId);
            throw;
        }
    }

    public async Task<List<ThirdPartyIssueReportedMessage>> PollEmailInboxAsync(string vendorId, CancellationToken cancellationToken = default)
    {
        var vendor = _vendorSettings.Vendors.FirstOrDefault(v => v.Id == vendorId);
        if (vendor == null)
        {
            _logger.LogWarning("Vendor {VendorId} not found for email polling", vendorId);
            return new List<ThirdPartyIssueReportedMessage>();
        }

        var issues = new List<ThirdPartyIssueReportedMessage>();

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(vendor.EmailSettings.ImapHost, vendor.EmailSettings.ImapPort, vendor.EmailSettings.UseStartTls, cancellationToken);
            await client.AuthenticateAsync(vendor.EmailSettings.Username, vendor.EmailSettings.Password, cancellationToken);

            await client.Inbox.OpenAsync(MailKit.FolderAccess.ReadWrite, cancellationToken);

            // Search for unread emails from vendor domain
            var query = SearchQuery.All.And(SearchQuery.Not(SearchQuery.Seen));
            if (!string.IsNullOrEmpty(vendor.EmailSettings.VendorDomain))
            {
                query = query.And(SearchQuery.FromContains(vendor.EmailSettings.VendorDomain));
            }

            var uids = await client.Inbox.SearchAsync(query, cancellationToken);

            foreach (var uid in uids)
            {
                var message = await client.Inbox.GetMessageAsync(uid, cancellationToken);
                var issue = ParseEmailToIssue(message, vendorId);
                
                if (issue != null)
                {
                    issues.Add(issue);
                    
                    // Mark as read and move to processed folder
                    await client.Inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                    
                    // Try to move to "Processed" folder
                    try
                    {
                        var processedFolder = await client.GetFolderAsync("Processed", cancellationToken);
                        await client.Inbox.MoveToAsync(uid, processedFolder, cancellationToken);
                    }
                    catch
                    {
                        // If folder doesn't exist, just leave marked as read
                        _logger.LogWarning("Could not move email to Processed folder for vendor {VendorId}", vendorId);
                    }
                }
            }

            await client.DisconnectAsync(true, cancellationToken);
            
            _logger.LogInformation("Polled {Count} issue emails for vendor {VendorId}", issues.Count, vendorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll email inbox for vendor {VendorId}", vendorId);
        }

        return issues;
    }

    private MimeMessage CreateDiscrepancyEmail(EmailDiscrepancyNotification notification, VendorConfiguration vendor)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("File Exchange System", vendor.EmailSettings.FromAddress));
        
        foreach (var recipient in vendor.EmailSettings.NotificationRecipients)
        {
            message.To.Add(new MailboxAddress("", recipient));
        }

        message.Subject = $"File Validation Discrepancies - {vendor.Name} - File {notification.FileId}";

        var bodyBuilder = new BodyBuilder();
        
        // Create HTML body
        var htmlBody = new StringBuilder();
        htmlBody.AppendLine($"<h2>File Validation Discrepancies Report</h2>");
        htmlBody.AppendLine($"<p><strong>Vendor:</strong> {vendor.Name}</p>");
        htmlBody.AppendLine($"<p><strong>File ID:</strong> {notification.FileId}</p>");
        htmlBody.AppendLine($"<p><strong>Correlation ID:</strong> {notification.CorrelationId}</p>");
        htmlBody.AppendLine($"<p><strong>Number of Discrepancies:</strong> {notification.Discrepancies.Count}</p>");
        htmlBody.AppendLine("<hr>");
        
        htmlBody.AppendLine("<table border='1' cellpadding='5' cellspacing='0'>");
        htmlBody.AppendLine("<tr><th>Record ID</th><th>Field</th><th>Rule Type</th><th>Expected</th><th>Actual</th><th>Description</th></tr>");
        
        foreach (var discrepancy in notification.Discrepancies)
        {
            htmlBody.AppendLine($"<tr>");
            htmlBody.AppendLine($"<td>{discrepancy.RecordId}</td>");
            htmlBody.AppendLine($"<td>{discrepancy.FieldName}</td>");
            htmlBody.AppendLine($"<td>{discrepancy.RuleType}</td>");
            htmlBody.AppendLine($"<td>{discrepancy.ExpectedValue}</td>");
            htmlBody.AppendLine($"<td>{discrepancy.ActualValue}</td>");
            htmlBody.AppendLine($"<td>{discrepancy.Description}</td>");
            htmlBody.AppendLine($"</tr>");
        }
        
        htmlBody.AppendLine("</table>");

        // Create plain text body
        var textBody = new StringBuilder();
        textBody.AppendLine("File Validation Discrepancies Report");
        textBody.AppendLine("====================================");
        textBody.AppendLine($"Vendor: {vendor.Name}");
        textBody.AppendLine($"File ID: {notification.FileId}");
        textBody.AppendLine($"Correlation ID: {notification.CorrelationId}");
        textBody.AppendLine($"Number of Discrepancies: {notification.Discrepancies.Count}");
        textBody.AppendLine();
        
        foreach (var discrepancy in notification.Discrepancies)
        {
            textBody.AppendLine($"Record: {discrepancy.RecordId}");
            textBody.AppendLine($"Field: {discrepancy.FieldName}");
            textBody.AppendLine($"Rule: {discrepancy.RuleType}");
            textBody.AppendLine($"Expected: {discrepancy.ExpectedValue}");
            textBody.AppendLine($"Actual: {discrepancy.ActualValue}");
            textBody.AppendLine($"Description: {discrepancy.Description}");
            textBody.AppendLine("---");
        }

        bodyBuilder.HtmlBody = htmlBody.ToString();
        bodyBuilder.TextBody = textBody.ToString();

        // Attach CSV report
        var csvContent = CreateDiscrepancyCsv(notification);
        var csvBytes = Encoding.UTF8.GetBytes(csvContent);
        bodyBuilder.Attachments.Add($"discrepancies_{notification.FileId}.csv", csvBytes, ContentType.Parse("text/csv"));

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private string CreateDiscrepancyCsv(EmailDiscrepancyNotification notification)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Record ID,Field Name,Rule Type,Expected Value,Actual Value,Description");
        
        foreach (var discrepancy in notification.Discrepancies)
        {
            csv.AppendLine($"\"{discrepancy.RecordId}\",\"{discrepancy.FieldName}\",\"{discrepancy.RuleType}\",\"{discrepancy.ExpectedValue}\",\"{discrepancy.ActualValue}\",\"{discrepancy.Description}\"");
        }
        
        return csv.ToString();
    }

    private ThirdPartyIssueReportedMessage? ParseEmailToIssue(MimeMessage message, string vendorId)
    {
        try
        {
            var subject = message.Subject ?? "";
            var body = message.GetTextBody(MimeKit.Text.TextFormat.Text) ?? message.GetTextBody(MimeKit.Text.TextFormat.Html) ?? "";
            
            // Try to extract file ID from subject or body
            var fileId = ExtractFileIdFromEmailContent(subject, body);
            
            return new ThirdPartyIssueReportedMessage
            {
                VendorId = vendorId,
                FileId = fileId ?? "unknown",
                IssueDescription = body.Length > 1000 ? body.Substring(0, 1000) + "..." : body,
                EmailSubject = subject,
                ReceivedAt = DateTimeOffset.UtcNow.ToString("O"),
                CorrelationId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse email to issue for vendor {VendorId}", vendorId);
            return null;
        }
    }

    private string? ExtractFileIdFromEmailContent(string subject, string body)
    {
        // Try common patterns for file ID extraction
        var patterns = new[]
        {
            @"file[:\s]+([a-zA-Z0-9\-_]+)",
            @"id[:\s]+([a-zA-Z0-9\-_]+)",
            @"reference[:\s]+([a-zA-Z0-9\-_]+)"
        };

        var content = $"{subject} {body}";
        
        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private async Task SendEmailAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();
        
        // Use configuration from the first vendor for SMTP settings (should be global)
        var smtpSettings = _vendorSettings.Vendors.FirstOrDefault()?.EmailSettings;
        if (smtpSettings == null)
        {
            throw new InvalidOperationException("No SMTP settings configured");
        }

        await client.ConnectAsync(smtpSettings.SmtpHost, smtpSettings.SmtpPort, smtpSettings.UseStartTls, cancellationToken);
        await client.AuthenticateAsync(smtpSettings.Username, smtpSettings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
