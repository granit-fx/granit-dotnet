using Granit.Core.Modularity;
using Granit.Guids;
using Granit.Http.ExceptionHandling;
using Granit.Persistence.Extensions;
using Granit.Security;
using Granit.Timing;

namespace Granit.Persistence;

/// <summary>
/// Granit module for EF Core interceptors (ISO 27001 audit trail + GDPR soft delete).
/// Depends on Timing (IClock), Guids (IGuidGenerator), and Security (ICurrentUserService).
/// ICurrentTenant is resolved via Granit.Core.MultiTenancy — Granit.MultiTenancy
/// is not a direct dependency of this module.
/// </summary>
[DependsOn(
    typeof(GranitExceptionHandlingModule),
    typeof(GranitGuidsModule),
    typeof(GranitSecurityModule),
    typeof(GranitTimingModule))]
public sealed class GranitPersistenceModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitPersistence();
}
