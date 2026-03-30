using Microsoft.AspNetCore.Http;

namespace Granit.Http.Cookies.Internal;

/// <summary>
/// Default <see cref="IGlobalPrivacyControlSignal"/> implementation that reads the
/// <c>Sec-GPC</c> HTTP header per the W3C GPC specification.
/// </summary>
internal sealed class GlobalPrivacyControlHeaderSignal : IGlobalPrivacyControlSignal
{
    private const string SecGpcHeader = "Sec-GPC";

    /// <inheritdoc/>
    public bool IsActive(HttpContext httpContext) =>
        httpContext.Request.Headers.TryGetValue(SecGpcHeader, out Microsoft.Extensions.Primitives.StringValues value)
        && string.Equals(value, "1", StringComparison.Ordinal);
}
