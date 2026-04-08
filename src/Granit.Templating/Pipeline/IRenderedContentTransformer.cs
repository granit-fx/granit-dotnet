using Granit.Templating.Keys;

namespace Granit.Templating.Pipeline;

/// <summary>
/// Transforms rendered text content after template engine rendering and layout wrapping.
/// </summary>
/// <remarks>
/// <para>
/// Transformers are executed as an ordered mutation chain: each transformer receives the
/// output of the previous one and returns modified content. The pipeline runs after
/// layout wrapping and before the content is consumed by callers.
/// </para>
/// <para>
/// Typical use cases:
/// <list type="bullet">
///   <item>MJML → email-safe HTML compilation (tables, inline CSS, MSO conditionals)</item>
///   <item>CSS inlining (moving <c>&lt;style&gt;</c> blocks to inline <c>style=""</c> attributes)</item>
///   <item>HTML minification</item>
/// </list>
/// </para>
/// <para>
/// Implementations are registered via DI and discovered automatically.
/// Use <see cref="Extensions.ServiceCollectionExtensions.AddRenderedContentTransformer{T}"/>
/// to register. Applications can add, replace, or remove transformers.
/// </para>
/// <example>
/// <code>
/// public sealed class MinificationTransformer : IRenderedContentTransformer
/// {
///     public int Order => 300;
///     public bool CanTransform(DocumentFormat format) => format == DocumentFormat.Html;
///     public Task&lt;string&gt; TransformAsync(string content, DocumentFormat format, CancellationToken ct)
///         => Task.FromResult(Minify(content));
/// }
/// </code>
/// </example>
/// </remarks>
public interface IRenderedContentTransformer
{
    /// <summary>
    /// Execution order (ascending). Lower values run first.
    /// Use multiples of 100 by convention to allow insertion without renumbering.
    /// </summary>
    /// <remarks>
    /// Recommended ranges:
    /// <list type="bullet">
    ///   <item>100 — structural transformation (MJML compilation)</item>
    ///   <item>200 — CSS processing (inlining, purging)</item>
    ///   <item>300+ — cosmetic (minification, cleanup)</item>
    /// </list>
    /// </remarks>
    int Order { get; }

    /// <summary>
    /// Determines whether this transformer should run for the given output format.
    /// </summary>
    /// <param name="format">The target document format.</param>
    /// <returns><see langword="true"/> if this transformer handles the format.</returns>
    bool CanTransform(DocumentFormat format);

    /// <summary>
    /// Transforms the rendered content.
    /// </summary>
    /// <param name="content">The current rendered text content.</param>
    /// <param name="format">The target document format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transformed content. Return <paramref name="content"/> unchanged when no transformation is needed.</returns>
    Task<string> TransformAsync(string content, DocumentFormat format, CancellationToken cancellationToken = default);
}
