using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Granit.Notifications.Smtp.Internal;

/// <summary>
/// Default <see cref="ISmtpTransport"/> implementation wrapping <see cref="SmtpClient"/>.
/// </summary>
internal sealed class MailKitSmtpTransport : ISmtpTransport
{
    private readonly SmtpClient _client = new();

    /// <inheritdoc />
    public int Timeout
    {
        get => _client.Timeout;
        set => _client.Timeout = value;
    }

    /// <inheritdoc />
    public Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) =>
        _client.ConnectAsync(host, port, options, cancellationToken);

    /// <inheritdoc />
    public Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default) =>
        _client.AuthenticateAsync(userName, password, cancellationToken);

    /// <inheritdoc />
    public Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default) =>
        _client.SendAsync(message, cancellationToken);

    /// <inheritdoc />
    public Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default) =>
        _client.DisconnectAsync(quit, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();
}
