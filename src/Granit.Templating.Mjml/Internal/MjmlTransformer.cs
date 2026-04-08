using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Mjml.Net;

namespace Granit.Templating.Mjml.Internal;

/// <summary>
/// Transforms MJML markup into email-client-safe HTML using the native <see cref="MjmlRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Only processes content that starts with <c>&lt;mjml</c> — plain HTML templates pass through
/// unchanged, ensuring backward compatibility.
/// </para>
/// <para>
/// Uses a single <see cref="MjmlRenderer"/> instance (thread-safe) to avoid per-call
/// allocation overhead in high-throughput scenarios.
/// </para>
/// </remarks>
internal sealed class MjmlTransformer : IRenderedContentTransformer
{
    private static readonly MjmlRenderer Renderer = new();

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public bool CanTransform(DocumentFormat format) => format == DocumentFormat.Html;

    /// <inheritdoc/>
    public Task<string> TransformAsync(string content, DocumentFormat format, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Only process MJML content — plain HTML passes through unchanged
        if (!IsMjml(content))
        {
            return Task.FromResult(content);
        }

        RenderResult result = Renderer.Render(content, new MjmlOptions
        {
            Beautify = false,
        });

        return Task.FromResult(result.Html);
    }

    private static bool IsMjml(string content) =>
        content.AsSpan().TrimStart().StartsWith("<mjml", StringComparison.OrdinalIgnoreCase);
}
