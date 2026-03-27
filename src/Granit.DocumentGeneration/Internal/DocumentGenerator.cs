using System.Diagnostics;
using Granit.DocumentGeneration.Diagnostics;
using Granit.DocumentGeneration.Exceptions;
using Granit.DocumentGeneration.Pipeline;
using Granit.MultiTenancy;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;

namespace Granit.DocumentGeneration.Internal;

/// <summary>
/// Internal implementation of <see cref="IDocumentGenerator"/>.
/// Coordinates the text rendering pipeline with the binary conversion step.
/// </summary>
internal sealed class DocumentGenerator(
    ITextTemplateRenderer textRenderer,
    IEnumerable<IDocumentRenderer> documentRenderers,
    DocumentGenerationMetrics metrics,
    ICurrentTenant currentTenant) : IDocumentGenerator
{
    private readonly ITextTemplateRenderer _textRenderer = textRenderer;
    private readonly IEnumerable<IDocumentRenderer> _documentRenderers = documentRenderers;

    /// <inheritdoc/>
    public async Task<DocumentResult> GenerateAsync<TData>(
        DocumentTemplateType<TData> templateType,
        TData data,
        DocumentFormat? targetFormat = null,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        DocumentFormat format = targetFormat ?? templateType.DefaultFormat;
        string templateTypeName = templateType.GetType().Name;
        string formatName = format.ToString();
        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        long startTimestamp = Stopwatch.GetTimestamp();
        bool succeeded = false;

        try
        {
            // 1. Render via the shared text pipeline (enrichers + resolver + engine).
            //    Binary engines (e.g. ClosedXML for Excel) return BinaryRenderedContent directly.
            RenderedContent content = await _textRenderer.RenderDocumentAsync(templateType, data, format, cancellationToken).ConfigureAwait(false);

            DocumentResult result;

            // 2a. Binary engine result: return directly, no IDocumentRenderer step needed.
            if (content is BinaryRenderedContent binary)
            {
                result = new DocumentResult(binary.Bytes, binary.Format);
            }
            else
            {
                // 2b. Text engine result: find a renderer that converts HTML → target format.
                var text = (TextRenderedContent)content;
                IDocumentRenderer? renderer = _documentRenderers.FirstOrDefault(r => r.CanRender(format));

                if (renderer is null)
                {
                    throw new DocumentRendererNotFoundException(format);
                }

                // 3. Convert HTML → binary document.
                result = await renderer.RenderAsync(text.Html, format, cancellationToken).ConfigureAwait(false);
            }

            succeeded = true;
            return result;
        }
        finally
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

            if (succeeded)
            {
                metrics.RecordDocumentGenerated(tenantId, templateTypeName, formatName);
            }
            else
            {
                metrics.RecordGenerationFailed(tenantId, templateTypeName, formatName);
            }

            metrics.RecordGenerationDuration(tenantId, templateTypeName, formatName, elapsed);
        }
    }
}
