using Granit.AuditLog;
using Granit.Modularity;
using Granit.Persistence;

namespace Granit.AuditLog.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of the audit trail.
/// </summary>
/// <remarks>
/// <para>
/// This module does not auto-register a DbContext connection — the host application
/// must call <c>builder.AddGranitAuditLogEntityFrameworkCore(configure)</c>.
/// </para>
/// <para>
/// The host application's DbContext must add the audit interceptor via
/// <c>options.UseGranitAuditLogInterceptor(sp)</c>.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuditLogModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitAuditLogEntityFrameworkCoreModule : GranitModule;
