using System.Diagnostics;

namespace Granit.ReferenceData.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.ReferenceData distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class ReferenceDataActivitySource
{
    /// <summary>The name of the Granit.ReferenceData <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.ReferenceData";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string GetAll = "reference-data.get-all";
    internal const string GetByCode = "reference-data.get-by-code";
    internal const string GetChildren = "reference-data.get-children";
    internal const string Create = "reference-data.create";
    internal const string Update = "reference-data.update";
    internal const string SetActive = "reference-data.set-active";
    internal const string Seed = "reference-data.seed";
}
