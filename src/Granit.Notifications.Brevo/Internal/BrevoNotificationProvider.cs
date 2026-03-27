using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Diagnostics;
using Granit.Notifications.Brevo.Options;
using Granit.Notifications.Email;
using Granit.Notifications.Sms;
using Granit.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Brevo.Internal;

/// <summary>
/// Unified Brevo provider implementing <see cref="IEmailSender"/>, <see cref="ISmsSender"/>,
/// and <see cref="IWhatsAppSender"/>. Uses Brevo Transactional API via <see cref="HttpClient"/>.
/// Registered as three Keyed Services with key "Brevo".
/// </summary>
internal sealed partial class BrevoNotificationProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<BrevoOptions> options,
    ILogger<BrevoNotificationProvider> logger) : IEmailSender, ISmsSender, IWhatsAppSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    async Task IEmailSender.SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        BrevoOptions opts = options.CurrentValue;
        HttpClient client = httpClientFactory.CreateClient("Brevo");

        object payload = new
        {
            sender = new
            {
                email = message.FromOverride ?? opts.DefaultSenderEmail,
                name = opts.DefaultSenderName,
            },
            to = new[] { new { email = message.To } },
            subject = message.Subject,
            htmlContent = message.HtmlBody,
            textContent = message.PlainTextBody,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "smtp/email", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync("smtp/email", response, cancellationToken).ConfigureAwait(false);

        LogEmailSent(LogRedaction.Email(message.To));
    }

    /// <inheritdoc />
    async Task ISmsSender.SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        BrevoOptions opts = options.CurrentValue;
        HttpClient client = httpClientFactory.CreateClient("Brevo");

        object payload = new
        {
            sender = message.SenderId ?? opts.DefaultSmsSenderId,
            recipient = message.To,
            content = message.Body,
            type = "transactional",
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "transactionalSMS/sms", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync("transactionalSMS/sms", response, cancellationToken).ConfigureAwait(false);

        LogSmsSent(LogRedaction.Phone(message.To));
    }

    /// <inheritdoc />
    async Task IWhatsAppSender.SendAsync(WhatsAppMessage message, CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("Brevo");

        object payload = new
        {
            senderNumber = (string?)null, // Brevo provides the sender number
            contactNumbers = new[] { message.To },
            templateId = message.TemplateName,
            @params = message.TemplateParameters,
            language = message.Language ?? "fr",
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "whatsapp/sendTemplate", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync("whatsapp/sendTemplate", response, cancellationToken).ConfigureAwait(false);

        LogWhatsAppSent(LogRedaction.Phone(message.To), message.TemplateName);
    }

    /// <summary>
    /// Reads the Brevo error body before throwing, so the caller (and logs) get a meaningful message.
    /// </summary>
    private async Task EnsureSuccessAsync(string endpoint, HttpResponseMessage response, CancellationToken cancellationToken)
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

        LogBrevoError(endpoint, (int)response.StatusCode, errorBody);

        throw new HttpRequestException(
            $"Brevo API error {(int)response.StatusCode} on {endpoint}: {errorBody ?? "(no body)"}",
            inner: null,
            response.StatusCode);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Brevo email sent to {RedactedRecipient}")]
    private partial void LogEmailSent(string redactedRecipient);

    [LoggerMessage(Level = LogLevel.Information, Message = "Brevo SMS sent to {RedactedRecipient}")]
    private partial void LogSmsSent(string redactedRecipient);

    [LoggerMessage(Level = LogLevel.Information, Message = "Brevo WhatsApp sent to {RedactedRecipient} template {TemplateName}")]
    private partial void LogWhatsAppSent(string redactedRecipient, string templateName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Brevo API error on {Endpoint}: HTTP {StatusCode} — {ErrorBody}")]
    private partial void LogBrevoError(string endpoint, int statusCode, string? errorBody);
}
