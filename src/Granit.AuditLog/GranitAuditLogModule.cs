using Granit.AuditLog.Extensions;
using Granit.Modularity;
using Granit.Querying;

namespace Granit.AuditLog;

/// <summary>
/// Granit module for audit trail management (ISO 27001 compliance).
/// Provides hierarchical audit log domain types, reader/writer abstractions,
/// category-based retention options, and diagnostics.
/// </summary>
/// <remarks>
/// Register via:
/// <code>
/// services.AddGranitAuditLog();
/// </code>
/// For EF Core persistence, add <c>Granit.AuditLog.EntityFrameworkCore</c>.
/// For admin endpoints, add <c>Granit.AuditLog.Endpoints</c>.
/// </remarks>
[DependsOn(typeof(GranitQueryingModule))]
public sealed class GranitAuditLogModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitAuditLog();
}
