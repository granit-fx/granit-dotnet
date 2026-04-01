using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Diagnostics;
using Granit.Notifications.Email.Scaleway.Diagnostics;
using Granit.Notifications.Email.Scaleway.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Email.Scaleway.Internal;

/// <summary>
/// <see cref="IEmailSender"/> implementation using Scaleway Transactional Email API.
/// Registered as Keyed Service with key "Scaleway".
/// </summary>
internal sealed partial class ScalewayEmailSender(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ScalewayEmailOptions> options,
    ILogger<ScalewayEmailSender> logger) : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ScalewayEmailOptions opts = options.CurrentValue;

        using Activity? activity = NotificationsEmailScalewayActivitySource.Source.StartActivity(
            NotificationsEmailScalewayActivitySource.Operations.Send);

        string fromEmail = message.FromEmailOverride ?? opts.DefaultSenderEmail;
        string fromName = message.FromNameOverride ?? opts.DefaultSenderName;

        object? textField = message.PlainTextBody is not null
            ? message.PlainTextBody
            : null;

        var to = new { email = message.To, name = message.ToName };

        object payload = new
        {
            from = new { email = fromEmail, name = fromName },
            to = new[] { to },
            subject = message.Subject,
            html = message.HtmlBody,
            text = textField,
            project_id = opts.ProjectId,
            additional_headers = message.Headers,
        };

        using HttpClient client = httpClientFactory.CreateClient("Scaleway");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "emails", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        LogEmailSent(LogRedaction.Email(message.To));
    }

    /// <summary>
    /// Reads the Scaleway error body before throwing, so the caller (and logs) get a meaningful message.
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

        LogScalewayError((int)response.StatusCode, errorBody);

        throw new HttpRequestException(
            $"Scaleway API error {(int)response.StatusCode} on emails: {errorBody ?? "(no body)"}",
            inner: null,
            response.StatusCode);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Scaleway email sent to {RedactedRecipient}")]
    private partial void LogEmailSent(string redactedRecipient);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scaleway API error: HTTP {StatusCode} — {ErrorBody}")]
    private partial void LogScalewayError(int statusCode, string? errorBody);
}
