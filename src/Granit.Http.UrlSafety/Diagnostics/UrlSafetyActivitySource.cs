using System.Diagnostics;

namespace Granit.Http.UrlSafety.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Http.UrlSafety distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class UrlSafetyActivitySource
{
    /// <summary>The name of the Granit.Http.UrlSafety <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Http.UrlSafety";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    internal const string ValidateUrl = "http_security.url.validate";
}
