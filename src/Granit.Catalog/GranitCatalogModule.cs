using Granit.Catalog.Extensions;
using Granit.Modularity;
using Granit.Workflow;

namespace Granit.Catalog;

/// <summary>
/// Granit module for the shared product catalog (Product aggregate, lifecycle, external mappings).
/// </summary>
/// <remarks>
/// MVP scope: Host-owned billing catalog. Products are referenced (soft, no SQL FK) by
/// <c>Granit.Metering.MeterDefinition.ProductId</c> and
/// <c>Granit.Subscriptions.PlanPrice.ProductId</c> to provide a stable join key for
/// invoice line items, audit trails, and external integrations.
/// Designed to grow toward multi-tenant e-commerce inventory in a future phase
/// (see ADR 032).
/// </remarks>
[DependsOn(typeof(GranitWorkflowModule))]
public sealed class GranitCatalogModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitCatalog();
}
