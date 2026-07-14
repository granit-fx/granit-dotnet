using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Http.Cookies.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of the cookie-consent ledger.
/// </summary>
/// <remarks>
/// This module does not auto-register a DbContext connection — the host application
/// must call <c>builder.AddGranitCookiesEntityFrameworkCore(configure)</c>, which also
/// replaces the base module's no-op <c>NullConsentLedger</c> with the durable
/// <c>EfCoreConsentLedger</c>.
/// </remarks>
[DependsOn(
    typeof(GranitHttpCookiesModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitHttpCookiesEntityFrameworkCoreModule : GranitModule;
