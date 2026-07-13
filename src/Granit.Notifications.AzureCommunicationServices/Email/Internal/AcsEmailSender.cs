using System.Diagnostics;
using Granit.Diagnostics;
using Granit.Notifications.AzureCommunicationServices.Email.Diagnostics;
using Granit.Notifications.AzureCommunicationServices.Email.Options;
using Granit.Notifications.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AzureCommunicationServices.Email.Internal;

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

        using Activity? activity = NotificationsAcsEmailActivitySource.Source.StartActivity(
            NotificationsAcsEmailActivitySource.Operations.SendEmail);
        activity?.SetTag(NotificationsAcsEmailActivitySource.Tags.To, LogRedaction.EmailDomain(message.To));
        activity?.SetTag(
            NotificationsAcsEmailActivitySource.Tags.SubjectLength,
            message.Subject.Length);

        string senderAddress = message.FromEmailOverride ?? opts.DefaultSenderEmail;

        var emailContent = new Azure.Communication.Email.EmailContent(message.Subject)
        {
            Html = message.HtmlBody,
        };

        if (message.PlainTextBody is not null)
        {
            emailContent.PlainText = message.PlainTextBody;
        }

        string? senderName = message.FromNameOverride ?? opts.DefaultSenderName;

        // ACS uses RFC 5322 formatted From (e.g. "Display Name <email@example.com>")
        string formattedSender = !string.IsNullOrEmpty(senderName)
            ? $"\"{senderName}\" <{senderAddress}>"
            : senderAddress;

        var acsMessage = new Azure.Communication.Email.EmailMessage(
            formattedSender,
            message.To,
            emailContent);

        // Custom headers (e.g. List-Unsubscribe)
        if (message.Headers is not null)
        {
            foreach (KeyValuePair<string, string> header in message.Headers)
            {
                acsMessage.Headers.Add(header.Key, header.Value);
            }
        }

        await transport.SendAsync(acsMessage, cancellationToken).ConfigureAwait(false);

        LogEmailSent(LogRedaction.Email(message.To));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ACS email sent to {RedactedRecipient}")]
    private partial void LogEmailSent(string redactedRecipient);
}
