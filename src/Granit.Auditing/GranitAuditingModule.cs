using Granit.Auditing.Extensions;
using Granit.DataExchange;
using Granit.Guids;
using Granit.Modularity;
using Granit.QueryEngine;

namespace Granit.Auditing;

/// <summary>
/// Granit module for audit trail management (ISO 27001 compliance).
/// Provides hierarchical audit log domain types, reader/writer abstractions,
/// category-based retention options, and diagnostics.
/// </summary>
/// <remarks>
/// Register via:
/// <code>
/// services.AddGranitAuditing();
/// </code>
/// For EF Core persistence, add <c>Granit.Auditing.EntityFrameworkCore</c>.
/// For admin endpoints, add <c>Granit.Auditing.Endpoints</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuditingAbstractionsModule),
    typeof(GranitDataExchangeAbstractionsModule),
    typeof(GranitGuidsModule),
    typeof(GranitQueryEngineAbstractionsModule))]
public sealed class GranitAuditingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitAuditing();
}
