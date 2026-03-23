using System.Diagnostics;

namespace Granit.Templating.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Templating distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class TemplatingActivitySource
{
    /// <summary>The name of the Granit.Templating <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Templating";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────
    internal const string Render = "templating.render";
    internal const string Resolve = "templating.resolve";
    internal const string SaveDraft = "templating.save-draft";
    internal const string Publish = "templating.publish";
    internal const string Unpublish = "templating.unpublish";
    internal const string DeleteDraft = "templating.delete-draft";
}
