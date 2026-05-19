using Granit.Caching;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of the audit trail.
/// </summary>
/// <remarks>
/// <para>
/// This module does not auto-register a DbContext connection — the host application
/// must call <c>builder.AddGranitAuditingEntityFrameworkCore(configure)</c>.
/// </para>
/// <para>
/// The host application's DbContext must add the audit interceptor via
/// <c>options.UseGranitAuditingInterceptor(sp)</c>.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuditingModule),
    typeof(GranitCachingModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitAuditingEntityFrameworkCoreModule : GranitModule;
