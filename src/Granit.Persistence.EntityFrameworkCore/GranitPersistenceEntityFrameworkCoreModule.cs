using Granit.Guids;
using Granit.Http.ExceptionHandling;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Granit.Timing;

namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core interceptors (ISO 27001 audit trail + GDPR soft delete).
/// Depends on Timing (IClock), Guids (IGuidGenerator), Http.ExceptionHandling
/// (EF Core → HTTP status mapping), QueryEngine.Abstractions (PagedResult) and the
/// Persistence abstractions (Specification).
/// ICurrentTenant is resolved via Granit.MultiTenancy — Granit.MultiTenancy
/// is not a direct dependency of this module.
/// </summary>
[DependsOn(
    typeof(GranitGuidsModule),
    typeof(GranitHttpExceptionHandlingModule),
    typeof(GranitPersistenceModule),
    typeof(GranitQueryEngineAbstractionsModule),
    typeof(GranitTimingModule))]
public sealed class GranitPersistenceEntityFrameworkCoreModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitDbDefaults.EnsureFromConfiguration(context.Configuration);
        context.Services.AddGranitPersistence();
    }
}
