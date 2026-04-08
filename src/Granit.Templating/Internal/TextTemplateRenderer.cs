using System.Globalization;
using Granit.Templating.Enrichment;
using Granit.Templating.Exceptions;
using Granit.Templating.GlobalContext;
using Granit.Templating.Keys;
using Granit.Templating.Layouts;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Templating.Internal;

/// <summary>
/// Internal implementation of <see cref="ITextTemplateRenderer"/>.
/// Orchestrates the full text rendering pipeline:
/// enrichment → resolution (with culture fallback) → layout wrapping → engine render.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Layout wrapping:</strong> when a layout is configured (via
/// <see cref="TemplateDescriptor.LayoutName"/> or <see cref="ILayoutRegistry"/>),
/// the renderer performs two-pass rendering:
/// <list type="number">
///   <item>Render the content template → HTML string</item>
///   <item>Render the layout template with <c>{{ body | raw }}</c> = content HTML</item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class TextTemplateRenderer(
    IEnumerable<ITemplateResolver> resolvers,
    IEnumerable<ITemplateEngine> engines,
    IEnumerable<ITemplateGlobalContext> globalContexts,
    IEnumerable<IRenderedContentTransformer> transformers,
    IServiceProvider serviceProvider,
    ILogger<TextTemplateRenderer> logger,
    ILayoutRegistry? layoutRegistry = null) : ITextTemplateRenderer
{
    private readonly IReadOnlyList<ITemplateResolver> _resolvers =
        [.. resolvers.OrderByDescending(r => r.Priority)];

    private readonly IReadOnlyList<ITemplateEngine> _engines = engines.ToList();

    private readonly IReadOnlyList<ITemplateGlobalContext> _globalContexts =
        globalContexts.ToList();

    private readonly IReadOnlyList<IRenderedContentTransformer> _transformers =
        [.. transformers.OrderBy(t => t.Order)];

    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <inheritdoc/>
    public async Task<RenderedTextResult> RenderAsync<TData>(
        TextTemplateType<TData> templateType,
        TData data,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        RenderedContent rendered = await RenderCoreAsync(templateType, data, DocumentFormat.Html, cancellationToken).ConfigureAwait(false);

        if (rendered is not TextRenderedContent text)
        {
            throw new InvalidOperationException(
                $"ITemplateEngine returned {rendered.GetType().Name} for a text template. " +
                "Ensure the registered ITemplateEngine supports 'text/html' templates.");
        }

        return new RenderedTextResult(text.Html);
    }

    /// <inheritdoc/>
    public Task<RenderedContent> RenderDocumentAsync<TData>(
        TextTemplateType<TData> templateType,
        TData data,
        DocumentFormat targetFormat,
        CancellationToken cancellationToken = default) where TData : notnull =>
        RenderCoreAsync(templateType, data, targetFormat, cancellationToken);

    private async Task<RenderedContent> RenderCoreAsync<TData>(
        TextTemplateType<TData> templateType,
        TData data,
        DocumentFormat targetFormat,
        CancellationToken cancellationToken) where TData : notnull
    {
        // 1. Enrich data (ordered, immutable)
        TData enrichedData = await EnrichAsync(data, cancellationToken).ConfigureAwait(false);

        // 2. Resolve template (culture-specific, then neutral fallback)
        string culture = CultureInfo.CurrentCulture.Name;
        TemplateDescriptor descriptor =
            await ResolveAsync(templateType.Name, culture, cancellationToken).ConfigureAwait(false)
            ?? throw new TemplateNotFoundException(templateType.Name, culture);

        // 3. Select engine by MIME type
        ITemplateEngine? engine = _engines.FirstOrDefault(e => e.CanRender(descriptor));

        if (engine is null)
        {
            throw new InvalidOperationException(
                $"No ITemplateEngine can render MIME type '{descriptor.MimeType}'. " +
                "Register a compatible engine (e.g. AddGranitTemplatingWithScriban or AddGranitDocumentGenerationExcel).");
        }

        // 4. Resolve layout (DB override → code registry → no layout)
        string? layoutName = descriptor.LayoutName
            ?? layoutRegistry?.GetLayoutName(templateType.Name);

        if (layoutName is null)
        {
            // No layout — render standalone (existing behavior)
            RenderedContent standaloneResult = await engine.RenderAsync(
                descriptor, enrichedData, targetFormat, _globalContexts, cancellationToken).ConfigureAwait(false);
            return await ApplyTransformersAsync(standaloneResult, targetFormat, cancellationToken).ConfigureAwait(false);
        }

        TemplateDescriptor? layoutDescriptor = await ResolveAsync(
            layoutName, culture, cancellationToken).ConfigureAwait(false);

        if (layoutDescriptor is null)
        {
            Log.LayoutNotFound(logger, layoutName, templateType.Name);
            RenderedContent noLayoutResult = await engine.RenderAsync(
                descriptor, enrichedData, targetFormat, _globalContexts, cancellationToken).ConfigureAwait(false);
            return await ApplyTransformersAsync(noLayoutResult, targetFormat, cancellationToken).ConfigureAwait(false);
        }

        // 5. Two-pass render: content first, then layout with body injection
        RenderedContent contentResult = await engine.RenderAsync(
            descriptor, enrichedData, targetFormat, _globalContexts, cancellationToken).ConfigureAwait(false);

        // Binary engines (Excel) skip layout wrapping — layouts are HTML-only
        if (contentResult is not TextRenderedContent contentText)
        {
            return contentResult;
        }

        // 6. Render layout with body = rendered content (pass 2 is always terminal)
        TemplateDescriptor layoutWithBody = new()
        {
            Content = layoutDescriptor.Content,
            MimeType = layoutDescriptor.MimeType,
            RevisionId = layoutDescriptor.RevisionId,
            ExtraVariables = new Dictionary<string, object> { ["body"] = contentText.Html },
        };

        RenderedContent layoutResult = await engine.RenderAsync(
            layoutWithBody, enrichedData, targetFormat, _globalContexts, cancellationToken).ConfigureAwait(false);
        return await ApplyTransformersAsync(layoutResult, targetFormat, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs all registered <see cref="IRenderedContentTransformer"/> instances in order
    /// on the rendered text content. Binary content is returned unchanged.
    /// </summary>
    private async Task<RenderedContent> ApplyTransformersAsync(
        RenderedContent content, DocumentFormat format, CancellationToken cancellationToken)
    {
        if (content is not TextRenderedContent textContent || _transformers.Count == 0)
        {
            return content;
        }

        string html = textContent.Html;
        foreach (IRenderedContentTransformer transformer in _transformers)
        {
            if (transformer.CanTransform(format))
            {
                html = await transformer.TransformAsync(html, format, cancellationToken).ConfigureAwait(false);
            }
        }

        return html == textContent.Html
            ? content
            : textContent with { Html = html };
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Layout template '{LayoutName}' not found for content template '{TemplateName}'. Rendering without layout.")]
        public static partial void LayoutNotFound(ILogger logger, string layoutName, string templateName);
    }

    private async Task<TData> EnrichAsync<TData>(TData data, CancellationToken cancellationToken)
        where TData : notnull
    {
        IEnumerable<ITemplateDataEnricher<TData>> enrichers =
            _serviceProvider.GetServices<ITemplateDataEnricher<TData>>();

        TData current = data;
        foreach (ITemplateDataEnricher<TData> enricher in enrichers.OrderBy(e => e.Order))
        {
            current = await enricher.EnrichAsync(current, cancellationToken).ConfigureAwait(false);
        }

        return current;
    }

    private async Task<TemplateDescriptor?> ResolveAsync(
        string name, string culture, CancellationToken cancellationToken)
    {
        // Culture-specific pass
        TemplateKey culturalKey = new(name, culture);
        foreach (ITemplateResolver resolver in _resolvers)
        {
            TemplateDescriptor? descriptor = await resolver.TryResolveAsync(culturalKey, cancellationToken).ConfigureAwait(false);
            if (descriptor is not null)
            {
                return descriptor;
            }
        }

        // Culture-neutral fallback
        TemplateKey neutralKey = new(name);
        foreach (ITemplateResolver resolver in _resolvers)
        {
            TemplateDescriptor? descriptor = await resolver.TryResolveAsync(neutralKey, cancellationToken).ConfigureAwait(false);
            if (descriptor is not null)
            {
                return descriptor;
            }
        }

        return null;
    }
}
