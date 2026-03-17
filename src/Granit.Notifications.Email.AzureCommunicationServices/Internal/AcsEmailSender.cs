using System.Diagnostics;
using Granit.Notifications.Email.AzureCommunicationServices.Diagnostics;
using Granit.Notifications.Email.AzureCommunicationServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Email.AzureCommunicationServices.Internal;

/// <summary>
/// <see cref="IEmailSender"/> implementation using Azure Communication Services.
/// Registered as Keyed Service with key "AzureCommunicationServices".
/// </summary>
internal sealed partial class AcsEmailSender(
    IAcsEmailTransport transport,
    IOptionsMonitor<AcsEmailOptions> options,
    ILogger<AcsEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        AcsEmailOptions opts = options.CurrentValue;

        using Activity? activity = NotificationsEmailAcsActivitySource.Source.StartActivity(
            NotificationsEmailAcsActivitySource.Operations.SendEmail);
        activity?.SetTag(NotificationsEmailAcsActivitySource.Tags.To, message.To);
        activity?.SetTag(
            NotificationsEmailAcsActivitySource.Tags.SubjectLength,
            message.Subject.Length);

        string senderAddress = message.FromOverride ?? opts.SenderAddress;

        var emailContent = new Azure.Communication.Email.EmailContent(message.Subject)
        {
            Html = message.HtmlBody,
        };

        if (message.PlainTextBody is not null)
        {
            emailContent.PlainText = message.PlainTextBody;
        }

        var acsMessage = new Azure.Communication.Email.EmailMessage(
            senderAddress,
            message.To,
            emailContent);

        await transport.SendAsync(acsMessage, cancellationToken).ConfigureAwait(false);

        LogEmailSent(message.To);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ACS email sent to {Recipient}")]
    private partial void LogEmailSent(string recipient);
}
