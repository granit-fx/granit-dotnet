using System.Diagnostics;

namespace Granit.Taxonomy.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Taxonomy distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class TaxonomyActivitySource
{
    /// <summary>The name of the Granit.Taxonomy <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Taxonomy";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────────────────────────────────────────────────────
    // Operations populate as the corresponding stories land.
    internal const string TagCreate = "taxonomy.tag.create";
    internal const string TagDelete = "taxonomy.tag.delete";
    internal const string TagList = "taxonomy.tag.list";
}
