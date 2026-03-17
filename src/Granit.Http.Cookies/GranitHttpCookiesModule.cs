using Granit.Core.Modularity;
using Granit.Timing;

namespace Granit.Http.Cookies;

/// <summary>
/// Granit module for RGPD-compliant cookie management.
/// Registration is done via <c>AddGranitCookies()</c> because it requires
/// an <see cref="System.Action{GranitCookiesBuilder}"/> for cookie declarations.
/// The dependency on <see cref="GranitTimingModule"/> guarantees that
/// <see cref="IClock"/> is available for cookie expiration calculation.
/// </summary>
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitHttpCookiesModule : GranitModule;
