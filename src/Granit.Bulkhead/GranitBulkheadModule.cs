using Granit.Bulkhead.Extensions;
using Granit.Features;
using Granit.Modularity;

namespace Granit.Bulkhead;

/// <summary>
/// Granit module for per-tenant bulkhead isolation (framework-pure core, ADR-062).
/// Registers <see cref="ConcurrencyLimiterRegistry"/>, <see cref="TenantPartitionedBulkhead"/>,
/// quota providers, and diagnostics from configuration section <c>"Bulkhead"</c>.
/// </summary>
/// <remarks>
/// Transport bindings live in <c>Granit.Http.Bulkhead</c> (ASP.NET Core endpoint filter +
/// RFC 7807 503 mapping) and <c>Granit.Bulkhead.Wolverine</c> (message pipeline middleware).
/// </remarks>
[DependsOn(typeof(GranitFeaturesModule))]
public sealed class GranitBulkheadModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitBulkhead();
}
