using Granit.Notifications.Abstractions;
using Granit.Notifications.Rendering;
using Granit.Templating.GlobalContext;
using Granit.Templating.Keys;
using Granit.Templating.Layouts;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications.Internal;

/// <summary>
/// Template-backed <see cref="INotificationContentRenderer"/> built on the
/// <c>Granit.Templating</c> pipeline (soft dependency — every collaborator is resolved
/// lazily, so hosts without templating simply get <see langword="null"/> and channels keep
/// their minimal fallback). Extracted verbatim from the email channel's private pipeline
/// (#2963) so every channel shares one localization/rendering path.
/// </summary>
/// <remarks>
/// <see cref="NotificationContentFormat.Html"/> only for now: two-tier template resolution
/// (<c>{typeName}</c> → <c>Notifications.Default</c>), effective culture
/// <c>context.Culture ?? recipient?.PreferredCulture</c> with culture-chain fallback,
/// layout wrap via <see cref="ILayoutRegistry"/> (MJML-aware body injection), subject
/// extracted from <c>&lt;title&gt;</c>. Text formats return <see langword="null"/> until
/// text-template support lands (#2963 follow-up).
/// </remarks>
internal sealed partial class TemplateNotificationContentRenderer(
    IServiceProvider serviceProvider,
    ILogger<TemplateNotificationContentRenderer> logger) : INotificationContentRenderer
{
    /// <summary>Channel-agnostic fallback template embedded in provider packages.</summary>
    internal const string FallbackTemplateName = "Notifications.Default";

    /// <inheritdoc />
    public async Task<RenderedNotificationContent?> RenderAsync(
        NotificationDeliveryContext context,
        RecipientInfo? recipient,
        NotificationContentFormat format,
        IReadOnlyDictionary<string, object?>? extraModelData = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Effective culture: explicit trigger override wins, then the recipient's preference.
        context = context with { Culture = context.Culture ?? recipient?.PreferredCulture };

        if (format != NotificationContentFormat.Html)
        {
            return await RenderTextAsync(context, recipient, format, extraModelData, cancellationToken).ConfigureAwait(false);
        }

        RenderedNotificationContent? content =
            await TryRenderTemplateAsync(context.NotificationTypeName, context, recipient, extraModelData, cancellationToken).ConfigureAwait(false)
            ?? await TryRenderTemplateAsync(FallbackTemplateName, context, recipient, extraModelData, cancellationToken).ConfigureAwait(false);

        if (content is null)
        {
            return null;
        }

        return await TryApplyLayoutAsync(context.NotificationTypeName, content, context, recipient, extraModelData, cancellationToken).ConfigureAwait(false)
            ?? content;
    }

    /// <summary>
    /// Text/markdown path: resolves <c>.txt</c>/<c>.md</c> template variants (via the
    /// <see cref="TemplateKey.MimeType"/> hint), no layout. Convention: the first non-empty
    /// line is the title (mirrors <c>&lt;title&gt;</c> for HTML), the remainder is the body.
    /// </summary>
    private async Task<RenderedNotificationContent?> RenderTextAsync(
        NotificationDeliveryContext context,
        RecipientInfo? recipient,
        NotificationContentFormat format,
        IReadOnlyDictionary<string, object?>? extraModelData,
        CancellationToken cancellationToken)
    {
        IEnumerable<ITemplateResolver>? resolvers = serviceProvider.GetService<IEnumerable<ITemplateResolver>>();
        if (resolvers?.Any() != true)
        {
            return null;
        }

        string mimeType = format == NotificationContentFormat.Markdown ? "text/markdown" : "text/plain";

        TemplateDescriptor? descriptor =
            await ResolveAsync(resolvers, new TemplateKey(context.NotificationTypeName, context.Culture, mimeType), cancellationToken).ConfigureAwait(false)
            ?? await ResolveAsync(resolvers, new TemplateKey(FallbackTemplateName, context.Culture, mimeType), cancellationToken).ConfigureAwait(false);

        if (descriptor is null)
        {
            return null;
        }

        ITemplateEngine? engine = FindEngine(descriptor, context.NotificationTypeName);
        if (engine is null)
        {
            return null;
        }

        try
        {
            RenderedContent rendered = await engine
                .RenderAsync(descriptor, BuildModel(context, recipient, extraModelData), DocumentFormat.Html, ResolveGlobalContexts(), cancellationToken)
                .ConfigureAwait(false);

            if (rendered is TextRenderedContent textResult)
            {
                Log.TemplateRendered(logger, context.NotificationTypeName, context.Culture);
                (string? title, string body) = SplitFirstLineTitle(textResult.Html);
                return new RenderedNotificationContent(title, body);
            }
        }
        catch (Exception ex)
        {
            Log.TemplateRenderFailed(logger, context.NotificationTypeName, ex);
        }

        return null;
    }

    /// <summary>First non-empty line = title, remainder = body (trimmed).</summary>
    internal static (string? Title, string Body) SplitFirstLineTitle(string text)
    {
        string trimmed = text.Trim();
        int newline = trimmed.IndexOf('\n');
        if (newline < 0)
        {
            return (null, trimmed);
        }

        string title = trimmed[..newline].TrimEnd('\r').Trim();
        string body = trimmed[(newline + 1)..].Trim();
        return (title.Length == 0 ? null : title, body.Length == 0 ? trimmed : body);
    }

    /// <summary>
    /// Attempts to resolve and render a template by name. Extracts the subject from the
    /// HTML <c>&lt;title&gt;</c> tag if present. Returns <see langword="null"/> if no
    /// template is found or templating is not configured.
    /// </summary>
    private async Task<RenderedNotificationContent?> TryRenderTemplateAsync(
        string templateName,
        NotificationDeliveryContext context,
        RecipientInfo? recipient,
        IReadOnlyDictionary<string, object?>? extraModelData,
        CancellationToken cancellationToken)
    {
        IEnumerable<ITemplateResolver>? resolvers = serviceProvider.GetService<IEnumerable<ITemplateResolver>>();
        if (resolvers?.Any() != true)
        {
            return null;
        }

        TemplateDescriptor? descriptor = await ResolveAsync(resolvers, new TemplateKey(templateName, context.Culture), cancellationToken).ConfigureAwait(false);
        if (descriptor is null)
        {
            return null;
        }

        ITemplateEngine? engine = FindEngine(descriptor, templateName);
        if (engine is null)
        {
            return null;
        }

        Dictionary<string, object?> dataDict = BuildModel(context, recipient, extraModelData);

        try
        {
            RenderedContent rendered = await engine
                .RenderAsync(descriptor, dataDict, DocumentFormat.Html, ResolveGlobalContexts(), cancellationToken)
                .ConfigureAwait(false);

            if (rendered is TextRenderedContent textResult)
            {
                Log.TemplateRendered(logger, templateName, context.Culture);
                (string? title, string body) = ExtractAndStripTitle(textResult.Html);
                return new RenderedNotificationContent(title, body);
            }
        }
        catch (Exception ex)
        {
            Log.TemplateRenderFailed(logger, templateName, ex);
        }

        return null;
    }

    /// <summary>
    /// Wraps rendered content in a layout template if one is registered for this notification
    /// type. Same two-pass pattern as <c>TextTemplateRenderer</c>: render layout with
    /// <c>{{ body }}</c> = pre-rendered content HTML.
    /// </summary>
    private async Task<RenderedNotificationContent?> TryApplyLayoutAsync(
        string templateName,
        RenderedNotificationContent content,
        NotificationDeliveryContext context,
        RecipientInfo? recipient,
        IReadOnlyDictionary<string, object?>? extraModelData,
        CancellationToken cancellationToken)
    {
        ILayoutRegistry? layoutRegistry = serviceProvider.GetService<ILayoutRegistry>();
        string? layoutName = layoutRegistry?.GetLayoutName(templateName);
        if (layoutName is null)
        {
            return null;
        }

        IEnumerable<ITemplateResolver>? resolvers = serviceProvider.GetService<IEnumerable<ITemplateResolver>>();
        if (resolvers is null)
        {
            return null;
        }

        TemplateDescriptor? layoutDescriptor =
            await ResolveAsync(resolvers, new TemplateKey(layoutName, context.Culture), cancellationToken).ConfigureAwait(false)
            ?? await ResolveAsync(resolvers, new TemplateKey(layoutName), cancellationToken).ConfigureAwait(false);

        if (layoutDescriptor is null)
        {
            Log.LayoutNotFound(logger, layoutName, templateName);
            return null;
        }

        ITemplateEngine? engine = FindEngine(layoutDescriptor, layoutName);
        if (engine is null)
        {
            return null;
        }

        Dictionary<string, object?> dataDict = BuildModel(context, recipient, extraModelData);

        // Inject title for {{ model.title }} in layout — use content <title> or humanized type name
        dataDict["title"] = content.Title ?? context.NotificationTypeName.Replace('.', ' ');

        // Detect MJML body fragment (starts with `<mj-`) vs plain HTML — layouts wrap HTML
        // bodies in a default `<mj-section><mj-column><mj-text>` so author writes plain markup,
        // and inject MJML bodies raw at the section level so authors can use `<mj-button>`,
        // `<mj-table>`, etc.
        bool bodyIsMjml = StartsWithMjml(content.Body);

        TemplateDescriptor layoutWithBody = new()
        {
            Content = layoutDescriptor.Content,
            MimeType = layoutDescriptor.MimeType,
            RevisionId = layoutDescriptor.RevisionId,
            ExtraVariables = new Dictionary<string, object>
            {
                ["body"] = content.Body,
                ["body_is_mjml"] = bodyIsMjml,
            },
        };

        try
        {
            RenderedContent rendered = await engine
                .RenderAsync(layoutWithBody, dataDict, DocumentFormat.Html, ResolveGlobalContexts(), cancellationToken)
                .ConfigureAwait(false);

            if (rendered is TextRenderedContent textResult)
            {
                // Subject was captured at content-render time; layout doesn't redefine it.
                return new RenderedNotificationContent(content.Title, textResult.Html);
            }
        }
        catch (Exception ex)
        {
            Log.TemplateRenderFailed(logger, layoutName, ex);
        }

        return null;
    }

    private static async Task<TemplateDescriptor?> ResolveAsync(
        IEnumerable<ITemplateResolver> resolvers, TemplateKey key, CancellationToken cancellationToken)
    {
        foreach (ITemplateResolver resolver in resolvers.OrderByDescending(r => r.Priority))
        {
            TemplateDescriptor? descriptor = await resolver.TryResolveAsync(key, cancellationToken).ConfigureAwait(false);
            if (descriptor is not null)
            {
                return descriptor;
            }
        }

        return null;
    }

    private ITemplateEngine? FindEngine(TemplateDescriptor descriptor, string templateName)
    {
        IEnumerable<ITemplateEngine>? engines = serviceProvider.GetService<IEnumerable<ITemplateEngine>>();
        ITemplateEngine? engine = engines?.FirstOrDefault(e => e.CanRender(descriptor));
        if (engine is null)
        {
            Log.NoEngineForTemplate(logger, templateName);
        }

        return engine;
    }

    private List<ITemplateGlobalContext> ResolveGlobalContexts() =>
        serviceProvider.GetService<IEnumerable<ITemplateGlobalContext>>()?.ToList() ?? [];

    /// <summary>
    /// Builds the template model: payload first, then channel-supplied extras, then generic
    /// enrichment defaults (TryAdd throughout — earlier keys always win).
    /// </summary>
    private Dictionary<string, object?> BuildModel(
        NotificationDeliveryContext context,
        RecipientInfo? recipient,
        IReadOnlyDictionary<string, object?>? extraModelData)
    {
        var dataDict = NotificationDataModel.ToDictionary(context.Data);

        if (extraModelData is not null)
        {
            foreach ((string key, object? value) in extraModelData)
            {
                dataDict.TryAdd(key, value);
            }
        }

        NotificationDefinition? definition = serviceProvider
            .GetService<INotificationDefinitionStore>()?.Get(context.NotificationTypeName);

        dataDict.TryAdd("notification_type", context.NotificationTypeName);
        dataDict.TryAdd("severity", context.Severity.ToString());
        dataDict.TryAdd("notification_group", definition?.GroupName ?? "");
        dataDict.TryAdd("allow_opt_out", definition?.AllowUserOptOut ?? true);
        dataDict.TryAdd("unsubscribe_url", "");
        dataDict.TryAdd("recipient_name", recipient?.DisplayName ?? "");
        dataDict.TryAdd("recipient_timezone", recipient?.PreferredTimeZone ?? "");

        return dataDict;
    }

    /// <summary>
    /// Returns whether the body is an MJML fragment, skipping any leading whitespace and HTML
    /// comments first. A translated template body begins with an <c>&lt;!-- AUTO-TRANSLATED --&gt;</c>
    /// marker, which must not fool the detection into treating the MJML fragment as plain HTML.
    /// </summary>
    internal static bool StartsWithMjml(string html)
    {
        ReadOnlySpan<char> span = html.AsSpan().TrimStart();

        while (span.StartsWith("<!--", StringComparison.Ordinal))
        {
            int end = span.IndexOf("-->", StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            span = span[(end + 3)..].TrimStart();
        }

        return span.StartsWith("<mj-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the content of the <c>&lt;title&gt;</c> tag from rendered HTML and returns
    /// the body with that tag removed. Used so the title doesn't leak into the layout's
    /// <c>&lt;mj-body&gt;</c> (where a raw <c>&lt;title&gt;</c> would either render as visible
    /// text or be silently dropped by MJML), while the subject is threaded explicitly.
    /// Leading HTML comments before the <c>&lt;title&gt;</c> (e.g. the
    /// <c>&lt;!-- AUTO-TRANSLATED --&gt;</c> marker stamped by
    /// <c>scripts/translate-templates.py</c>) are stripped too — same skipping as
    /// <see cref="StartsWithMjml"/> — so template metadata never leaks into the rendered body.
    /// </summary>
    internal static (string? Title, string Body) ExtractAndStripTitle(string html)
    {
        int start = html.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return (null, html);
        }

        int contentStart = start + "<title>".Length;
        int contentEnd = html.IndexOf("</title>", contentStart, StringComparison.OrdinalIgnoreCase);
        if (contentEnd < 0)
        {
            return (null, html);
        }

        string title = html[contentStart..contentEnd].Trim();
        int after = contentEnd + "</title>".Length;

        // Consume a single trailing newline so the body doesn't start with a blank line.
        if (after < html.Length && html[after] == '\r')
        {
            after++;
        }
        if (after < html.Length && html[after] == '\n')
        {
            after++;
        }

        int prefixStart = SkipLeadingCommentsAndWhitespace(html);
        string body = prefixStart >= start
            ? html[after..]
            : html[prefixStart..start] + html[after..];
        return (string.IsNullOrEmpty(title) ? null : title, body);
    }

    /// <summary>
    /// Returns the index of the first character that is neither leading whitespace nor part
    /// of a complete leading HTML comment. Mirrors the comment-skipping in
    /// <see cref="StartsWithMjml"/>.
    /// </summary>
    private static int SkipLeadingCommentsAndWhitespace(string html)
    {
        int pos = 0;
        while (true)
        {
            while (pos < html.Length && char.IsWhiteSpace(html[pos]))
            {
                pos++;
            }

            if (!html.AsSpan(pos).StartsWith("<!--", StringComparison.Ordinal))
            {
                return pos;
            }

            int end = html.IndexOf("-->", pos, StringComparison.Ordinal);
            if (end < 0)
            {
                return pos;
            }

            pos = end + 3;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug,
            Message = "Rendered notification template '{NotificationType}' (culture: {Culture}).")]
        public static partial void TemplateRendered(ILogger logger, string notificationType, string? culture);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "No template engine can render template for notification '{NotificationType}'.")]
        public static partial void NoEngineForTemplate(ILogger logger, string notificationType);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Failed to render notification template for '{NotificationType}'.")]
        public static partial void TemplateRenderFailed(ILogger logger, string notificationType, Exception exception);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Layout template '{LayoutName}' not found for notification '{NotificationType}'. Rendering without layout.")]
        public static partial void LayoutNotFound(ILogger logger, string layoutName, string notificationType);
    }
}
