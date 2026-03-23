using System.Diagnostics;

namespace Granit.Localization.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Localization distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class LocalizationActivitySource
{
    /// <summary>The name of the Granit.Localization <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Localization";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string ResolveResource = "localization.resolve-resource";
    internal const string SetOverride = "localization.set-override";
    internal const string RemoveOverride = "localization.remove-override";
}
