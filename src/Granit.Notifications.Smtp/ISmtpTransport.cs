using MailKit.Security;
using MimeKit;

namespace Granit.Notifications.Smtp;

/// <summary>
/// Thin abstraction over <see cref="MailKit.Net.Smtp.SmtpClient"/> to allow
/// unit testing without a real SMTP server.
/// </summary>
internal interface ISmtpTransport : IDisposable
{
    /// <summary>Connection and send timeout in milliseconds.</summary>
    int Timeout { get; set; }

    /// <summary>Connects to the SMTP server.</summary>
    Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default);

    /// <summary>Authenticates with the SMTP server.</summary>
    Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default);

    /// <summary>Sends a MIME message.</summary>
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default);

    /// <summary>Disconnects from the SMTP server.</summary>
    Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default);
}
