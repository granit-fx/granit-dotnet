using Granit.Bulkhead;
using Granit.Http.Bulkhead.Extensions;
using Granit.Http.ExceptionHandling;
using Granit.Modularity;

namespace Granit.Http.Bulkhead;

/// <summary>
/// Granit module for the ASP.NET Core binding of bulkhead isolation. Depends on the
/// framework-pure <see cref="GranitBulkheadModule"/> (registry, quota providers,
/// <see cref="TenantPartitionedBulkhead"/>) and adds the RFC 7807 mapping of
/// <see cref="Granit.Bulkhead.Exceptions.BulkheadRejectedException"/> to HTTP 503.
/// </summary>
[DependsOn(
    typeof(GranitBulkheadModule),
    typeof(GranitHttpExceptionHandlingModule))]
public sealed class GranitHttpBulkheadModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitHttpBulkhead();
}
