using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Granit.Notifications.Brevo.Diagnostics;
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
        using Activity? activity = NotificationsBrevoActivitySource.Source.StartActivity(NotificationsBrevoActivitySource.Operations.SendEmail);
        BrevoOptions opts = options.CurrentValue;
        HttpClient client = httpClientFactory.CreateClient("Brevo");

        var to = new { email = message.To, name = message.ToName };

        object payload = new
        {
            sender = new
            {
                email = message.FromEmailOverride ?? opts.DefaultSenderEmail,
                name = message.FromNameOverride ?? opts.DefaultSenderName,
            },
            to = new[] { to },
            subject = message.Subject,
            htmlContent = message.HtmlBody,
            textContent = message.PlainTextBody,
            headers = message.Headers,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "smtp/email", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
        await response.EnsureGranitSuccessAsync(logger, "Brevo", "smtp/email", cancellationToken).ConfigureAwait(false);

        LogEmailSent(LogRedaction.Email(message.To));
    }

    /// <inheritdoc />
    async Task ISmsSender.SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        using Activity? activity = NotificationsBrevoActivitySource.Source.StartActivity(NotificationsBrevoActivitySource.Operations.SendSms);
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
        await response.EnsureGranitSuccessAsync(logger, "Brevo", "transactionalSMS/sms", cancellationToken).ConfigureAwait(false);

        LogSmsSent(LogRedaction.Phone(message.To));
    }

    /// <inheritdoc />
    async Task IWhatsAppSender.SendAsync(WhatsAppMessage message, CancellationToken cancellationToken)
    {
        using Activity? activity = NotificationsBrevoActivitySource.Source.StartActivity(NotificationsBrevoActivitySource.Operations.SendWhatsApp);
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
        await response.EnsureGranitSuccessAsync(logger, "Brevo", "whatsapp/sendTemplate", cancellationToken).ConfigureAwait(false);

        LogWhatsAppSent(LogRedaction.Phone(message.To), message.TemplateName);
    }


    [LoggerMessage(Level = LogLevel.Information, Message = "Brevo email sent to {RedactedRecipient}")]
    private partial void LogEmailSent(string redactedRecipient);

    [LoggerMessage(Level = LogLevel.Information, Message = "Brevo SMS sent to {RedactedRecipient}")]
    private partial void LogSmsSent(string redactedRecipient);

    [LoggerMessage(Level = LogLevel.Information, Message = "Brevo WhatsApp sent to {RedactedRecipient} template {TemplateName}")]
    private partial void LogWhatsAppSent(string redactedRecipient, string templateName);

}
