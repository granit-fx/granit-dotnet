using System.Diagnostics;
using Amazon.SimpleEmailV2.Model;
using Granit.Diagnostics;
using Granit.Notifications.Email.AwsSes.Diagnostics;
using Granit.Notifications.Email.AwsSes.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Email.AwsSes.Internal;

/// <summary>
/// <see cref="IEmailSender"/> implementation using Amazon SES v2.
/// Registered as Keyed Service with key "AwsSes".
/// </summary>
internal sealed partial class AwsSesEmailSender(
    IOptionsMonitor<AwsSesOptions> options,
    ILogger<AwsSesEmailSender> logger,
    Func<IAwsSesTransport>? transportFactory = null) : IEmailSender
{
    private readonly Func<IAwsSesTransport> _transportFactory =
        transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        AwsSesOptions ses = options.CurrentValue;

        using Activity? activity = NotificationsEmailAwsSesActivitySource.Source.StartActivity(
            NotificationsEmailAwsSesActivitySource.Operations.SendEmail);
        activity?.SetTag(NotificationsEmailAwsSesActivitySource.Tags.Region, ses.Region);

        string fromEmail = message.FromEmailOverride ?? ses.DefaultSenderEmail ?? "noreply@localhost";
        string? fromName = message.FromNameOverride ?? ses.DefaultSenderName;

        // SES v2 uses RFC 5322 formatted From (e.g. "Display Name <email@example.com>")
        string fromAddress = !string.IsNullOrEmpty(fromName)
            ? $"\"{fromName}\" <{fromEmail}>"
            : fromEmail;

        EmailContent content = new()
        {
            Simple = new Message
            {
                Subject = new Content { Data = message.Subject },
                Body = new Body
                {
                    Html = new Content { Data = message.HtmlBody },
                },
                Headers = BuildMessageHeaders(message.Headers),
            },
        };

        if (message.PlainTextBody is not null)
        {
            content.Simple.Body.Text = new Content { Data = message.PlainTextBody };
        }

        SendEmailRequest request = new()
        {
            FromEmailAddress = fromAddress,
            Destination = new Destination { ToAddresses = [message.To] },
            Content = content,
        };

        if (ses.ConfigurationSetName is not null)
        {
            request.ConfigurationSetName = ses.ConfigurationSetName;
        }

        using IAwsSesTransport transport = _transportFactory();
        await transport.SendEmailAsync(request, cancellationToken).ConfigureAwait(false);

        LogEmailSent(LogRedaction.Email(message.To), ses.Region);
    }

    private static List<MessageHeader>? BuildMessageHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return null;
        }

        List<MessageHeader> result = new(headers.Count);
        foreach (KeyValuePair<string, string> header in headers)
        {
            result.Add(new MessageHeader { Name = header.Key, Value = header.Value });
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SES email sent to {RedactedRecipient} via {Region}")]
    private partial void LogEmailSent(string redactedRecipient, string region);
}
