
using SecureFileExchange.Contracts;

namespace SecureFileExchange.Services;

public interface IEmailService
{
    Task SendDiscrepancyNotificationAsync(EmailDiscrepancyNotification notification, CancellationToken cancellationToken = default);
    Task<List<ThirdPartyIssueReportedMessage>> PollEmailInboxAsync(string vendorId, CancellationToken cancellationToken = default);
}
