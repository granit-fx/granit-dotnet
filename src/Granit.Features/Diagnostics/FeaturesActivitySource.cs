using System.Diagnostics;

namespace Granit.Features.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Features distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class FeaturesActivitySource
{
    /// <summary>The name of the Granit.Features <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Features";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    internal const string ResolveValue = "features.resolve-value";
    internal const string SetOverride = "features.set-override";
    internal const string DeleteOverride = "features.delete-override";
    internal const string CheckLimit = "features.check-limit";
}
