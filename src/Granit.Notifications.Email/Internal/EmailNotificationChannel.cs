using Granit.Html;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Email.Options;
using Granit.Notifications.Rendering;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Timing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Email.Internal;

/// <summary>
/// Email notification channel that resolves the provider at runtime via Keyed Services
/// and renders Scriban templates when available.
/// </summary>
/// <remarks>
/// <para>
/// Template resolution order:
/// <list type="number">
///   <item>Looks for a template named <c>{NotificationTypeName}</c>
///     (e.g., <c>"Security.Welcome"</c>) via any registered <see cref="ITemplateResolver"/>.</item>
///   <item>Falls back to the built-in <c>Notifications.Default</c> embedded template
///     (localized in 16 cultures) when no type-specific template exists.</item>
/// </list>
/// Both templates use Scriban syntax with <c>{{ model.* }}</c> for data binding.
/// The fallback template additionally exposes <c>{{ model.notification_type }}</c>.
/// </para>
/// <para>
/// Templates can be provided as embedded resources (<c>AddEmbeddedTemplates()</c>),
/// stored in the database (<c>Granit.Templating.EntityFrameworkCore</c>), or
/// any custom <see cref="ITemplateResolver"/>.
/// </para>
/// <para>
/// When <see cref="INotificationDefinitionStore"/> is available and the notification
/// has <c>AllowUserOptOut = true</c>, RFC 2369 / RFC 8058 List-Unsubscribe headers
/// are injected into the <see cref="EmailMessage.Headers"/>.
/// </para>
/// </remarks>
internal sealed partial class EmailNotificationChannel(
    IServiceProvider serviceProvider,
    IOptions<EmailChannelOptions> options,
    IRecipientResolver recipientResolver,
    IConfiguration configuration,
    ICurrentTimezoneProvider timezoneProvider,
    [FromKeyedServices(HtmlConverterKeys.Trusted)] IHtmlToPlainTextConverter htmlToPlainText,
    ILogger<EmailNotificationChannel> logger,
    INotificationContentRenderer? contentRenderer = null) : INotificationChannel
{
    /// <summary>
    /// Name of the built-in fallback template embedded in this assembly.
    /// Resolved when no type-specific template exists for the notification.
    /// </summary>
    internal const string FallbackTemplateName = "Notifications.Default";

    /// <inheritdoc />
    public string Name => NotificationChannels.Email;

    /// <inheritdoc />
    public async Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        IEmailSender sender = serviceProvider.GetRequiredKeyedService<IEmailSender>(options.Value.Provider);

        RecipientInfo? recipient = context.RecipientOverride
            ?? await recipientResolver.ResolveAsync(context.RecipientUserId, cancellationToken).ConfigureAwait(false);
        if (recipient?.Email is null)
        {
            return;
        }

        // Validate email format (defense against misuse of RecipientOverride)
        if (!System.Net.Mail.MailAddress.TryCreate(recipient.Email, out _))
        {
            Log.InvalidRecipientEmail(logger, context.NotificationTypeName);
            return;
        }


        // Resolve notification metadata for opt-out and group info
        INotificationDefinitionStore? defStore = serviceProvider.GetService<INotificationDefinitionStore>();
        NotificationDefinition? definition = defStore?.Get(context.NotificationTypeName);
        bool allowOptOut = definition?.AllowUserOptOut ?? true;

        // Build List-Unsubscribe headers (RFC 2369 + RFC 8058) when opt-out is allowed
        Dictionary<string, string>? headers = null;
        string unsubscribeUrl = "";
        if (allowOptOut)
        {
            unsubscribeUrl = await ResolveUnsubscribeUrlAsync(cancellationToken).ConfigureAwait(false);
            if (unsubscribeUrl.Length > 0)
            {
                headers = new()
                {
                    ["List-Unsubscribe"] = $"<{unsubscribeUrl}>",
                    ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
                };
            }
        }

        // Render in the recipient's time zone so {{ to_user_time }} localizes timestamps.
        // ICurrentTimezoneProvider is AsyncLocal — set it, render, restore in finally.
        string? previousTz = timezoneProvider.Timezone;
        timezoneProvider.Timezone = recipient.PreferredTimeZone;

        RenderedNotificationContent? rendered;
        try
        {
            // Shared pipeline (two-tier resolution, culture chain, layout wrap, <title>
            // subject) — the email channel only adds its transport-specific variables.
            Dictionary<string, object?> channelData = new()
            {
                ["allow_opt_out"] = allowOptOut,
                ["unsubscribe_url"] = allowOptOut ? unsubscribeUrl : "",
            };

            rendered = contentRenderer is null
                ? null
                : await contentRenderer.RenderAsync(
                    context, recipient, NotificationContentFormat.Html, channelData, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            timezoneProvider.Timezone = previousTz;
        }

        string subject = rendered?.Title ?? context.NotificationTypeName.Replace('.', ' ');
        string htmlBody = rendered?.Body
            ?? $"<p>{context.NotificationTypeName}</p>";

        // Apply content transformers (MJML → HTML, CSS inlining, etc.)
        htmlBody = await ApplyTransformersAsync(htmlBody, cancellationToken).ConfigureAwait(false);

        string plainTextBody = await htmlToPlainText
            .ConvertAsync(htmlBody, cancellationToken).ConfigureAwait(false);

        EmailChannelOptions opts = options.Value;

        await sender.SendAsync(new EmailMessage
        {
            To = recipient.Email,
            ToName = recipient.DisplayName,
            Subject = subject,
            HtmlBody = htmlBody,
            PlainTextBody = plainTextBody,
            FromEmailOverride = opts.DefaultSenderEmail,
            FromNameOverride = opts.DefaultSenderName,
            Headers = headers,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the unsubscribe URL from <see cref="EmailChannelOptions.UnsubscribeUrl"/>,
    /// then tenant-aware URL, then static configuration fallback.
    /// </summary>
    private async Task<string> ResolveUnsubscribeUrlAsync(CancellationToken cancellationToken)
    {
        string? url = options.Value.UnsubscribeUrl;
        if (!string.IsNullOrEmpty(url))
        {
            return url;
        }

        // Soft dependency: use tenant-aware URL when multi-tenancy is configured
        ITenantUrlResolver? urlResolver = serviceProvider.GetService<ITenantUrlResolver>();
        string? resolvedUrl = urlResolver is not null
            ? await urlResolver.ResolveBaseUrlAsync(cancellationToken).ConfigureAwait(false)
            : null;
        string? baseUrl = !string.IsNullOrEmpty(resolvedUrl) ? resolvedUrl : configuration["Templating:App:BaseUrl"];

        return string.IsNullOrEmpty(baseUrl) ? "" : baseUrl.TrimEnd('/') + "/notifications/preferences";
    }

    /// <summary>
    /// Runs all registered <see cref="IRenderedContentTransformer"/> instances (e.g. MJML → HTML)
    /// on the final HTML body, mirroring the pipeline in <c>TextTemplateRenderer</c>.
    /// </summary>
    private async Task<string> ApplyTransformersAsync(string html, CancellationToken cancellationToken)
    {
        IEnumerable<IRenderedContentTransformer>? transformers =
            serviceProvider.GetService<IEnumerable<IRenderedContentTransformer>>();

        if (transformers is null)
        {
            return html;
        }

        string result = html;
        foreach (IRenderedContentTransformer transformer in transformers.Where(t => t.CanTransform(DocumentFormat.Html)).OrderBy(t => t.Order))
        {
            result = await transformer.TransformAsync(result, DocumentFormat.Html, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Invalid recipient email format for notification '{NotificationType}'. Skipping delivery.")]
        public static partial void InvalidRecipientEmail(ILogger logger, string notificationType);

    }
}
