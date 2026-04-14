using System.Text.Json;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Email.Options;
using Granit.Templating.Keys;
using Granit.Templating.Layouts;
using Granit.Templating.Pipeline;
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
    ILogger<EmailNotificationChannel> logger) : INotificationChannel
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

        // Build enrichment context for templates
        EmailEnrichment enrichment = new(definition, allowOptOut, unsubscribeUrl);

        // Try type-specific template first, then fall back to the built-in default template
        RenderedEmail? rendered = await TryRenderTemplateAsync(
                context.NotificationTypeName, context, enrichment, cancellationToken).ConfigureAwait(false)
            ?? await TryRenderTemplateAsync(
                FallbackTemplateName, context, enrichment, cancellationToken).ConfigureAwait(false);

        // Apply layout wrapping (two-pass: content was rendered above, now wrap in layout)
        if (rendered is not null)
        {
            rendered = await TryApplyLayoutAsync(
                context.NotificationTypeName, rendered, context, enrichment, cancellationToken).ConfigureAwait(false)
                ?? rendered;
        }

        string subject = rendered?.Subject ?? context.NotificationTypeName.Replace('.', ' ');
        string htmlBody = rendered?.Html
            ?? $"<p>{context.NotificationTypeName}</p>";

        // Apply content transformers (MJML → HTML, CSS inlining, etc.)
        htmlBody = await ApplyTransformersAsync(htmlBody, cancellationToken).ConfigureAwait(false);

        string plainTextBody = await HtmlToPlainTextConverter
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
        string? baseUrl = !string.IsNullOrEmpty(resolvedUrl) ? resolvedUrl : configuration["Granit:Templating:App:BaseUrl"];

        return string.IsNullOrEmpty(baseUrl) ? "" : baseUrl.TrimEnd('/') + "/notifications/preferences";
    }

    /// <summary>
    /// Attempts to resolve and render a Scriban template by name.
    /// Extracts the subject from the HTML <c>&lt;title&gt;</c> tag if present.
    /// Returns <see langword="null"/> if no template is found or templating is not configured.
    /// </summary>
    private async Task<RenderedEmail?> TryRenderTemplateAsync(
        string templateName, NotificationDeliveryContext context, EmailEnrichment enrichment, CancellationToken cancellationToken)
    {
        // Resolve ITemplateResolver chain (optional — not all apps have Granit.Templating)
        IEnumerable<ITemplateResolver>? resolvers = serviceProvider.GetService<IEnumerable<ITemplateResolver>>();
        if (resolvers is null || !resolvers.Any())
        {
            return null;
        }

        TemplateKey key = new(templateName, context.Culture);

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
            Log.NoEngineForTemplate(logger, templateName);
            return null;
        }

        // Convert JsonElement data to Dictionary for Scriban rendering + enrich with metadata
        Dictionary<string, object?> dataDict = JsonElementToDictionary(context.Data);
        EnrichModelData(dataDict, context, enrichment);

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
                Log.TemplateRendered(logger, templateName, context.Culture);
                string? subject = ExtractTitleFromHtml(textResult.Html);
                return new RenderedEmail(textResult.Html, subject);
            }
        }
        catch (Exception ex)
        {
            Log.TemplateRenderFailed(logger, templateName, ex);
        }

        return null;
    }

    /// <summary>
    /// Wraps rendered content in a layout template if one is registered for this notification type.
    /// Uses the same two-pass pattern as <c>TextTemplateRenderer</c>: render layout with
    /// <c>{{ body }}</c> = pre-rendered content HTML.
    /// </summary>
    private async Task<RenderedEmail?> TryApplyLayoutAsync(
        string templateName,
        RenderedEmail contentEmail,
        NotificationDeliveryContext context,
        EmailEnrichment enrichment,
        CancellationToken cancellationToken)
    {
        // Resolve layout name from registry (code-level defaults)
        ILayoutRegistry? layoutRegistry = serviceProvider.GetService<ILayoutRegistry>();
        string? layoutName = layoutRegistry?.GetLayoutName(templateName);

        if (layoutName is null)
        {
            return null;
        }

        // Resolve the layout template
        IEnumerable<ITemplateResolver>? resolvers = serviceProvider.GetService<IEnumerable<ITemplateResolver>>();
        if (resolvers is null)
        {
            return null;
        }

        TemplateKey layoutKey = new(layoutName, context.Culture);
        TemplateDescriptor? layoutDescriptor = null;
        foreach (ITemplateResolver resolver in resolvers.OrderByDescending(r => r.Priority))
        {
            layoutDescriptor = await resolver.TryResolveAsync(layoutKey, cancellationToken).ConfigureAwait(false);
            if (layoutDescriptor is not null)
            {
                break;
            }
        }

        // Culture-neutral fallback for layout
        if (layoutDescriptor is null)
        {
            TemplateKey neutralKey = new(layoutName);
            foreach (ITemplateResolver resolver in resolvers.OrderByDescending(r => r.Priority))
            {
                layoutDescriptor = await resolver.TryResolveAsync(neutralKey, cancellationToken).ConfigureAwait(false);
                if (layoutDescriptor is not null)
                {
                    break;
                }
            }
        }

        if (layoutDescriptor is null)
        {
            Log.LayoutNotFound(logger, layoutName, templateName);
            return null;
        }

        // Render layout with body injection
        IEnumerable<ITemplateEngine>? engines = serviceProvider.GetService<IEnumerable<ITemplateEngine>>();
        ITemplateEngine? engine = engines?.FirstOrDefault(e => e.CanRender(layoutDescriptor));
        if (engine is null)
        {
            return null;
        }

        Dictionary<string, object?> dataDict = JsonElementToDictionary(context.Data);
        EnrichModelData(dataDict, context, enrichment);

        // Inject title for {{ model.title }} in layout — use content <title> or humanized type name
        dataDict["title"] = contentEmail.Subject
            ?? context.NotificationTypeName.Replace('.', ' ');

        // Inject body as ExtraVariable for {{ body | raw }} in layout
        TemplateDescriptor layoutWithBody = new()
        {
            Content = layoutDescriptor.Content,
            MimeType = layoutDescriptor.MimeType,
            RevisionId = layoutDescriptor.RevisionId,
            ExtraVariables = new Dictionary<string, object> { ["body"] = contentEmail.Html },
        };

        try
        {
            IReadOnlyList<Granit.Templating.GlobalContext.ITemplateGlobalContext> globalContexts =
                serviceProvider.GetService<IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>>()?.ToList()
                ?? [];

            RenderedContent rendered = await engine
                .RenderAsync(layoutWithBody, dataDict, DocumentFormat.Html, globalContexts, cancellationToken)
                .ConfigureAwait(false);

            if (rendered is TextRenderedContent textResult)
            {
                string? subject = ExtractTitleFromHtml(textResult.Html);
                return new RenderedEmail(textResult.Html, subject);
            }
        }
        catch (Exception ex)
        {
            Log.TemplateRenderFailed(logger, layoutName, ex);
        }

        return null;
    }

    /// <summary>
    /// Enriches the template model data with notification metadata for the footer.
    /// Uses defensive defaults (empty string / false) to avoid null issues in Scriban.
    /// </summary>
    private static void EnrichModelData(
        Dictionary<string, object?> dataDict,
        NotificationDeliveryContext context,
        EmailEnrichment enrichment)
    {
        dataDict.TryAdd("notification_type", context.NotificationTypeName);
        dataDict.TryAdd("notification_group", enrichment.Definition?.GroupName ?? "");
        dataDict.TryAdd("allow_opt_out", enrichment.AllowOptOut);
        dataDict.TryAdd("unsubscribe_url", enrichment.AllowOptOut ? enrichment.UnsubscribeUrl : "");
    }

    /// <summary>Notification metadata resolved once per send, threaded through render methods.</summary>
    private sealed record EmailEnrichment(
        NotificationDefinition? Definition,
        bool AllowOptOut,
        string UnsubscribeUrl);

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

    /// <summary>Result of rendering a Scriban email template.</summary>
    private sealed record RenderedEmail(string Html, string? Subject);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug,
            Message = "Rendered email template '{NotificationType}' (culture: {Culture}).")]
        public static partial void TemplateRendered(ILogger logger, string notificationType, string? culture);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Invalid recipient email format for notification '{NotificationType}'. Skipping delivery.")]
        public static partial void InvalidRecipientEmail(ILogger logger, string notificationType);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "No template engine can render template for notification '{NotificationType}'.")]
        public static partial void NoEngineForTemplate(ILogger logger, string notificationType);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Failed to render email template for notification '{NotificationType}'.")]
        public static partial void TemplateRenderFailed(ILogger logger, string notificationType, Exception exception);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Layout template '{LayoutName}' not found for notification '{NotificationType}'. Sending without layout.")]
        public static partial void LayoutNotFound(ILogger logger, string layoutName, string notificationType);
    }
}
