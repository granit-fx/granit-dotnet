using Granit.Notifications.Email.Smtp.Options;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Granit.Notifications.Email.Smtp.Internal;

/// <summary>
/// <see cref="IEmailSender"/> implementation using MailKit SMTP.
/// Registered as Keyed Service with key "Smtp".
/// </summary>
internal sealed partial class MailKitEmailSender(
    IOptionsMonitor<SmtpOptions> options,
    ILogger<MailKitEmailSender> logger,
    Func<ISmtpTransport>? transportFactory = null) : IEmailSender
{
    private readonly Func<ISmtpTransport> _transportFactory = transportFactory ?? (() => new MailKitSmtpTransport());

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        SmtpOptions smtp = options.CurrentValue;
        int timeoutMs = smtp.TimeoutSeconds * 1000;

        MimeMessage mimeMessage = new();
        mimeMessage.From.Add(new MailboxAddress(
            message.FromOverride ?? smtp.Username ?? "noreply",
            message.FromOverride ?? smtp.Username ?? "noreply@localhost"));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;

        BodyBuilder bodyBuilder = new()
        {
            HtmlBody = message.HtmlBody,
        };

        if (message.PlainTextBody is not null)
        {
            bodyBuilder.TextBody = message.PlainTextBody;
        }

        mimeMessage.Body = bodyBuilder.ToMessageBody();

        using ISmtpTransport client = _transportFactory();
        client.Timeout = timeoutMs;

        SecureSocketOptions socketOptions = smtp.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, cancellationToken).ConfigureAwait(false);

        if (smtp.Username is not null && smtp.Password is not null)
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);

        LogEmailSent(message.To, smtp.Host, smtp.Port);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMTP email sent to {Recipient} via {Host}:{Port}")]
    private partial void LogEmailSent(string recipient, string host, int port);
}
