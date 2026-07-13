using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Granit.Notifications.Email;
using Granit.Notifications.SendGrid.Diagnostics;
using Granit.Notifications.SendGrid.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.SendGrid.Internal;

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

        using Activity? activity = NotificationsSendGridActivitySource.Source.StartActivity(
            NotificationsSendGridActivitySource.Operations.Send);

        string fromEmail = message.FromEmailOverride ?? opts.DefaultSenderEmail;
        string fromName = message.FromNameOverride ?? opts.DefaultSenderName;

        List<object> content = [new { type = "text/html", value = message.HtmlBody }];

        if (message.PlainTextBody is not null)
        {
            content.Insert(0, new { type = "text/plain", value = message.PlainTextBody });
        }

        var to = new { email = message.To, name = message.ToName };

        object payload = new
        {
            personalizations = new[] { new { to = new[] { to } } },
            from = new { email = fromEmail, name = fromName },
            subject = message.Subject,
            content,
            headers = message.Headers,
        };

        using HttpClient client = httpClientFactory.CreateClient("SendGrid");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "mail/send", payload, JsonOptions, cancellationToken).ConfigureAwait(false);
        await response.EnsureGranitSuccessAsync(logger, "SendGrid", "v3/mail/send", cancellationToken).ConfigureAwait(false);

        LogEmailSent(LogRedaction.Email(message.To));
    }


    [LoggerMessage(Level = LogLevel.Information, Message = "SendGrid email sent to {RedactedRecipient}")]
    private partial void LogEmailSent(string redactedRecipient);

}
