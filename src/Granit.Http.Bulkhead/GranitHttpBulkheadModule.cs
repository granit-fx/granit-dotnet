using Granit.Features;
using Granit.Http.Bulkhead.Extensions;
using Granit.Http.ExceptionHandling;
using Granit.Modularity;
using Granit.Users;

namespace Granit.Http.Bulkhead;

/// <summary>
/// Granit module for per-tenant bulkhead isolation.
/// Registers <see cref="Internal.ConcurrencyLimiterRegistry"/>, <see cref="Internal.TenantPartitionedBulkhead"/>,
/// and all required dependencies from configuration section <c>"Bulkhead"</c>.
/// </summary>
[DependsOn(
    typeof(GranitFeaturesModule),
    typeof(GranitHttpExceptionHandlingModule))]
public sealed class GranitHttpBulkheadModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitBulkhead();
}
