using Granit.Guids;
using Granit.Http.ExceptionHandling;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Timing;
using Granit.Users;

namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core interceptors (ISO 27001 audit trail + GDPR soft delete).
/// Depends on Timing (IClock), Guids (IGuidGenerator), and Security (ICurrentUserService).
/// ICurrentTenant is resolved via Granit.MultiTenancy — Granit.MultiTenancy
/// is not a direct dependency of this module.
/// </summary>
[DependsOn(
    typeof(GranitHttpExceptionHandlingModule),
    typeof(GranitGuidsModule),
    typeof(GranitTimingModule))]
public sealed class GranitPersistenceEntityFrameworkCoreModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitDbDefaults.EnsureFromConfiguration(context.Configuration);
        context.Services.AddGranitPersistence();
    }
}
