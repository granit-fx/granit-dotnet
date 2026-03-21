using System.Text.Json;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Email.Options;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
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
/// Template resolution: looks for a template named <c>{NotificationTypeName}</c>
/// (e.g., <c>"Security.Welcome"</c>) via any registered <see cref="ITemplateResolver"/>.
/// If found, renders with Scriban using the notification data as <c>{{ model.* }}</c>.
/// If not found, falls back to a generic notification email.
/// </para>
/// <para>
/// Templates can be provided as embedded resources (<c>AddEmbeddedTemplates()</c>),
/// stored in the database (<c>Granit.Templating.EntityFrameworkCore</c>), or
/// any custom <see cref="ITemplateResolver"/>.
/// </para>
/// </remarks>
internal sealed partial class EmailNotificationChannel(
    IServiceProvider serviceProvider,
    IOptions<EmailChannelOptions> options,
    IRecipientResolver recipientResolver,
    ILogger<EmailNotificationChannel> logger) : INotificationChannel
{
    /// <inheritdoc />
    public string Name => NotificationChannels.Email;

    /// <inheritdoc />
    public async Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        IEmailSender sender = serviceProvider.GetRequiredKeyedService<IEmailSender>(options.Value.Provider);

        RecipientInfo? recipient = await recipientResolver.ResolveAsync(context.RecipientUserId, cancellationToken).ConfigureAwait(false);
        if (recipient?.Email is null)
        {
            return;
        }

        // Try to render a Scriban template for this notification type
        RenderedEmail? rendered = await TryRenderTemplateAsync(context, cancellationToken).ConfigureAwait(false);

        string subject = rendered?.Subject ?? context.NotificationTypeName.Replace('.', ' ');
        string htmlBody = rendered?.Html
            ?? $"<p>You have a new notification of type <strong>{context.NotificationTypeName}</strong>.</p>";

        await sender.SendAsync(new EmailMessage
        {
            To = recipient.Email,
            Subject = subject,
            HtmlBody = htmlBody,
            FromOverride = options.Value.SenderAddress,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to resolve and render a Scriban template named after the notification type.
    /// Extracts the subject from the HTML <c>&lt;title&gt;</c> tag if present.
    /// Returns <see langword="null"/> if no template is found or templating is not configured.
    /// </summary>
    private async Task<RenderedEmail?> TryRenderTemplateAsync(
        NotificationDeliveryContext context, CancellationToken cancellationToken)
    {
        // Resolve ITemplateResolver chain (optional — not all apps have Granit.Templating)
        IEnumerable<ITemplateResolver>? resolvers = serviceProvider.GetService<IEnumerable<ITemplateResolver>>();
        if (resolvers is null || !resolvers.Any())
        {
            return null;
        }

        TemplateKey key = new(context.NotificationTypeName, context.Culture);

        // Try each resolver in priority order
        TemplateDescriptor? descriptor = null;
        foreach (ITemplateResolver resolver in resolvers.OrderByDescending(r => r.Priority))
        {
            descriptor = await resolver.TryResolveAsync(key, cancellationToken).ConfigureAwait(false);
            if (descriptor is not null)
            {
                break;
            }
        }

        if (descriptor is null)
        {
            return null;
        }

        // Resolve a template engine (Scriban)
        IEnumerable<ITemplateEngine>? engines = serviceProvider.GetService<IEnumerable<ITemplateEngine>>();
        ITemplateEngine? engine = engines?.FirstOrDefault(e => e.CanRender(descriptor));
        if (engine is null)
        {
            Log.NoEngineForTemplate(logger, context.NotificationTypeName);
            return null;
        }

        // Convert JsonElement data to Dictionary for Scriban rendering
        Dictionary<string, object?> dataDict = JsonElementToDictionary(context.Data);

        try
        {
            IReadOnlyList<Granit.Templating.GlobalContext.ITemplateGlobalContext> globalContexts =
                serviceProvider.GetService<IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>>()?.ToList()
                ?? [];

            RenderedContent rendered = await engine
                .RenderAsync(descriptor, dataDict, DocumentFormat.Html, globalContexts, cancellationToken)
                .ConfigureAwait(false);

            if (rendered is TextRenderedContent textResult)
            {
                Log.TemplateRendered(logger, context.NotificationTypeName, context.Culture);
                string? subject = ExtractTitleFromHtml(textResult.Html);
                return new RenderedEmail(textResult.Html, subject);
            }
        }
        catch (Exception ex)
        {
            Log.TemplateRenderFailed(logger, context.NotificationTypeName, ex);
        }

        return null;
    }

    private static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        Dictionary<string, object?> dict = [];
        if (element.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (JsonProperty prop in element.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.ToString(),
            };
        }

        return dict;
    }

    /// <summary>
    /// Extracts the content of the <c>&lt;title&gt;</c> tag from rendered HTML, if present.
    /// Used as the email subject when a Scriban template provides it.
    /// </summary>
    private static string? ExtractTitleFromHtml(string html)
    {
        int start = html.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += "<title>".Length;
        int end = html.IndexOf("</title>", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
        {
            return null;
        }

        string title = html[start..end].Trim();
        return string.IsNullOrEmpty(title) ? null : title;
    }

    /// <summary>Result of rendering a Scriban email template.</summary>
    private sealed record RenderedEmail(string Html, string? Subject);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug,
            Message = "Rendered email template '{NotificationType}' (culture: {Culture}).")]
        public static partial void TemplateRendered(ILogger logger, string notificationType, string? culture);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "No template engine can render template for notification '{NotificationType}'.")]
        public static partial void NoEngineForTemplate(ILogger logger, string notificationType);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Failed to render email template for notification '{NotificationType}'.")]
        public static partial void TemplateRenderFailed(ILogger logger, string notificationType, Exception exception);
    }
}
