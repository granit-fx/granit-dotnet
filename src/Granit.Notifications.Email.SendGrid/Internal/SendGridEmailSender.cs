using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Diagnostics;
using Granit.Notifications.Email.SendGrid.Diagnostics;
using Granit.Notifications.Email.SendGrid.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Email.SendGrid.Internal;

/// <summary>
/// <see cref="IEmailSender"/> implementation using SendGrid v3 API.
/// Registered as Keyed Service with key "SendGrid".
/// </summary>
internal sealed partial class SendGridEmailSender(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<SendGridEmailOptions> options,
    ILogger<SendGridEmailSender> logger) : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        SendGridEmailOptions opts = options.CurrentValue;

        using Activity? activity = NotificationsEmailSendGridActivitySource.Source.StartActivity(
            NotificationsEmailSendGridActivitySource.Operations.Send);

        string fromEmail = message.FromOverride ?? opts.DefaultSenderEmail;
        string fromName = opts.DefaultSenderName;

        List<object> content = [new { type = "text/html", value = message.HtmlBody }];

        if (message.PlainTextBody is not null)
        {
            content.Insert(0, new { type = "text/plain", value = message.PlainTextBody });
        }

        object payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = message.To } } } },
            from = new { email = fromEmail, name = fromName },
            subject = message.Subject,
            content,
        };

        using HttpClient client = httpClientFactory.CreateClient("SendGrid");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "mail/send", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        LogEmailSent(LogRedaction.Email(message.To));
    }

    /// <summary>
    /// Reads the SendGrid error body before throwing, so the caller (and logs) get a meaningful message.
    /// </summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? errorBody = null;
        try
        {
            errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort — do not mask the original HTTP error.
        }

        LogSendGridError((int)response.StatusCode, errorBody);

        throw new HttpRequestException(
            $"SendGrid API error {(int)response.StatusCode} on mail/send: {errorBody ?? "(no body)"}",
            inner: null,
            response.StatusCode);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SendGrid email sent to {RedactedRecipient}")]
    private partial void LogEmailSent(string redactedRecipient);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SendGrid API error: HTTP {StatusCode} — {ErrorBody}")]
    private partial void LogSendGridError(int statusCode, string? errorBody);
}
