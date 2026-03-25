using Granit.Modularity;

namespace Granit.Http.Cookies;

/// <summary>
/// Granit module for RGPD-compliant cookie management.
/// Registration is done via <c>AddGranitCookies()</c> because it requires
/// an <see cref="System.Action{GranitCookiesBuilder}"/> for cookie declarations.
/// </summary>
public sealed class GranitHttpCookiesModule : GranitModule;
