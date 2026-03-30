using Granit.Http.Cookies;
using Granit.Modularity;

namespace Granit.Privacy.Regulations.Cookies;

/// <summary>
/// Bridge module connecting <c>Granit.Privacy.Regulations</c> to <c>Granit.Http.Cookies</c>.
/// Provides regulation-aware consent model resolution for GPC enforcement and
/// opt-in/opt-out cookie handling.
/// </summary>
[DependsOn(
    typeof(GranitHttpCookiesModule),
    typeof(GranitPrivacyRegulationsModule))]
public sealed class GranitPrivacyRegulationsCookiesModule : GranitModule;
