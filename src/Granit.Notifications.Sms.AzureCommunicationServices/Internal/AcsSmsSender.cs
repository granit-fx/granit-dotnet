using System.Diagnostics;
using Azure.Communication.Sms;
using Granit.Notifications.Sms.AzureCommunicationServices.Diagnostics;
using Granit.Notifications.Sms.AzureCommunicationServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Sms.AzureCommunicationServices.Internal;

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

        using Activity? activity = NotificationsSmsAcsActivitySource.Source.StartActivity(
            NotificationsSmsAcsActivitySource.Operations.SendSms);
        activity?.SetTag(NotificationsSmsAcsActivitySource.Tags.Recipient, message.To);

        string fromNumber = message.SenderId ?? opts.FromPhoneNumber;

        SmsSendResult result = await transport
            .SendAsync(fromNumber, message.To, message.Body, cancellationToken)
            .ConfigureAwait(false);

        if (result.Successful)
        {
            LogSmsSent(message.To, result.MessageId);
        }
        else
        {
            LogSmsFailed(message.To, result.ErrorMessage);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ACS SMS sent to {Recipient}, messageId={MessageId}")]
    private partial void LogSmsSent(string recipient, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ACS SMS to {Recipient} failed: {ErrorMessage}")]
    private partial void LogSmsFailed(string recipient, string errorMessage);
}
