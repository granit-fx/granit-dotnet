using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Granit.Notifications.Email;
using Granit.Notifications.Scaleway.Diagnostics;
using Granit.Notifications.Scaleway.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Scaleway.Internal;

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

        using Activity? activity = NotificationsScalewayActivitySource.Source.StartActivity(
            NotificationsScalewayActivitySource.Operations.Send);

        string fromEmail = message.FromEmailOverride ?? opts.DefaultSenderEmail;
        string fromName = message.FromNameOverride ?? opts.DefaultSenderName;

        object? textField = message.PlainTextBody;

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
        await response.EnsureGranitSuccessAsync(logger, "Scaleway", "emails", cancellationToken).ConfigureAwait(false);

        LogEmailSent(LogRedaction.Email(message.To));
    }


    [LoggerMessage(Level = LogLevel.Information, Message = "Scaleway email sent to {RedactedRecipient}")]
    private partial void LogEmailSent(string redactedRecipient);

}
