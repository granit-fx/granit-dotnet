using System.Diagnostics;
using Azure.Communication.Sms;
using Granit.Diagnostics;
using Granit.Notifications.AzureCommunicationServices.Sms.Diagnostics;
using Granit.Notifications.AzureCommunicationServices.Sms.Options;
using Granit.Notifications.Sms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AzureCommunicationServices.Sms.Internal;

/// <summary>
/// <see cref="ISmsSender"/> implementation using Azure Communication Services.
/// Registered as Keyed Service with key "AzureCommunicationServices".
/// </summary>
internal sealed partial class AcsSmsSender(
    IOptionsMonitor<AcsSmsOptions> options,
    ILogger<AcsSmsSender> logger,
    IAcsSmsTransport transport) : ISmsSender
{
    /// <inheritdoc />
    public async Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        AcsSmsOptions opts = options.CurrentValue;

        using Activity? activity = NotificationsAcsSmsActivitySource.Source.StartActivity(
            NotificationsAcsSmsActivitySource.Operations.SendSms);
        activity?.SetTag(NotificationsAcsSmsActivitySource.Tags.Recipient, LogRedaction.HashPrefix(message.To));

        string fromNumber = message.SenderId ?? opts.FromPhoneNumber;

        SmsSendResult result = await transport
            .SendAsync(fromNumber, message.To, message.Body, cancellationToken)
            .ConfigureAwait(false);

        if (result.Successful)
        {
            LogSmsSent(LogRedaction.Phone(message.To), result.MessageId);
        }
        else
        {
            LogSmsFailed(LogRedaction.Phone(message.To), result.ErrorMessage);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ACS SMS sent to {RedactedRecipient}, messageId={MessageId}")]
    private partial void LogSmsSent(string redactedRecipient, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ACS SMS to {RedactedRecipient} failed: {ErrorMessage}")]
    private partial void LogSmsFailed(string redactedRecipient, string errorMessage);
}
