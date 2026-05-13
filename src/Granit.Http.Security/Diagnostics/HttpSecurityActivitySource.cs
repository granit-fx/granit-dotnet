using System.Diagnostics;

namespace Granit.Http.Security.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Http.Security distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class HttpSecurityActivitySource
{
    /// <summary>The name of the Granit.Http.Security <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.HttpSecurity";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    internal const string ValidateUrl = "http_security.url.validate";
}
