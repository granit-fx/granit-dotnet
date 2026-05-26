using System.Diagnostics;

namespace Granit.LanguageDetection.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for <c>Granit.LanguageDetection</c> distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class LanguageDetectionActivitySource
{
    /// <summary>The name of the <c>Granit.LanguageDetection</c> <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.LanguageDetection";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    internal const string Detect = "language_detection.detect";
}
