using Microsoft.AspNetCore.Http;

namespace Granit.Http.Cookies;

/// <summary>
/// Detects whether the Global Privacy Control (GPC) signal is active in the request.
/// The GPC signal (<c>Sec-GPC: 1</c>) is a W3C standard that indicates the user
/// prefers not to have their data sold or shared.
/// </summary>
/// <seealso href="https://globalprivacycontrol.github.io/gpc-spec/"/>
public interface IGlobalPrivacyControlSignal
{
    /// <summary>
    /// Returns <c>true</c> if the <c>Sec-GPC</c> header is set to <c>"1"</c>.
    /// </summary>
    bool IsActive(HttpContext httpContext);
}
