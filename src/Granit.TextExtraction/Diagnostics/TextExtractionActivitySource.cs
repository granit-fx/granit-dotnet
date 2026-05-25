using System.Diagnostics;

namespace Granit.TextExtraction.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for <c>Granit.TextExtraction</c> distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class TextExtractionActivitySource
{
    /// <summary>The name of the <c>Granit.TextExtraction</c> <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.TextExtraction";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    internal const string Extract = "text_extraction.extract";
}
