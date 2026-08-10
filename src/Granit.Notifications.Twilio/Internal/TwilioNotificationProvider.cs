using System.Diagnostics;
using Granit.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Granit.Notifications.Sms;
using Granit.Notifications.Twilio.Diagnostics;
using Granit.Notifications.Twilio.Options;
using Granit.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Twilio.Internal;

/// <summary>
/// Unified Twilio provider implementing <see cref="ISmsSender"/> and <see cref="IWhatsAppSender"/>.
/// Uses Twilio Messaging API via <see cref="HttpClient"/> with form-encoded payloads.
/// Registered as two Keyed Services with key "Twilio".
/// </summary>
internal sealed partial class TwilioNotificationProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<TwilioOptions> options,
    ILogger<TwilioNotificationProvider> logger) : ISmsSender, IWhatsAppSender
{
    /// <summary>Keyed-service and named-<see cref="HttpClient"/> key.</summary>
    private const string ProviderName = "Twilio";

    /// <inheritdoc />
    async Task ISmsSender.SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        using Activity? activity = NotificationsTwilioActivitySource.Source.StartActivity(NotificationsTwilioActivitySource.Operations.SendSms);
        TwilioOptions opts = options.CurrentValue;
        HttpClient client = httpClientFactory.CreateClient(ProviderName);

        string fromNumber = message.SenderId ?? opts.DefaultSmsFromNumber;
        string endpoint = $"2010-04-01/Accounts/{opts.AccountSid}/Messages.json";

        FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["From"] = fromNumber,
            ["To"] = message.To,
            ["Body"] = message.Body,
        });

        using HttpResponseMessage response = await client.PostAsync(
            endpoint, content, cancellationToken).ConfigureAwait(false);
        await response.EnsureGranitSuccessAsync(logger, ProviderName, endpoint, cancellationToken).ConfigureAwait(false);

        LogSmsSent(LogRedaction.Phone(message.To));
    }

    /// <inheritdoc />
    async Task IWhatsAppSender.SendAsync(WhatsAppMessage message, CancellationToken cancellationToken)
    {
        using Activity? activity = NotificationsTwilioActivitySource.Source.StartActivity(NotificationsTwilioActivitySource.Operations.SendWhatsApp);
        TwilioOptions opts = options.CurrentValue;
        HttpClient client = httpClientFactory.CreateClient(ProviderName);

        string whatsAppNumber = opts.DefaultWhatsAppFromNumber ?? opts.DefaultSmsFromNumber;
        string endpoint = $"2010-04-01/Accounts/{opts.AccountSid}/Messages.json";

        string body = BuildWhatsAppBody(message);

        FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["From"] = $"whatsapp:{whatsAppNumber}",
            ["To"] = $"whatsapp:{message.To}",
            ["Body"] = body,
        });

        using HttpResponseMessage response = await client.PostAsync(
            endpoint, content, cancellationToken).ConfigureAwait(false);
        await response.EnsureGranitSuccessAsync(logger, ProviderName, endpoint, cancellationToken).ConfigureAwait(false);

        LogWhatsAppSent(LogRedaction.Phone(message.To), message.TemplateName);
    }

    /// <summary>
    /// Builds the WhatsApp message body from template name and optional parameters.
    /// When parameters are present, they are interpolated into the template name using positional placeholders.
    /// </summary>
    private static string BuildWhatsAppBody(WhatsAppMessage message)
    {
        if (message.TemplateParameters is not { Count: > 0 })
        {
            return message.TemplateName;
        }

        return string.Join(", ", message.TemplateParameters.Prepend(message.TemplateName));
    }


    [LoggerMessage(Level = LogLevel.Information, Message = "Twilio SMS sent to {RedactedRecipient}")]
    private partial void LogSmsSent(string redactedRecipient);

    [LoggerMessage(Level = LogLevel.Information, Message = "Twilio WhatsApp sent to {RedactedRecipient} template {TemplateName}")]
    private partial void LogWhatsAppSent(string redactedRecipient, string templateName);

}
