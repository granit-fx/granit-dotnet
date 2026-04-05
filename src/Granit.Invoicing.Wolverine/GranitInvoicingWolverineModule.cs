using Granit.Invoicing.Wolverine.Internal;
using Granit.Modularity;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Invoicing.Wolverine;

/// <summary>Wolverine integration for Granit.Invoicing.</summary>
[DependsOn(
    typeof(GranitInvoicingModule),
    typeof(GranitWolverineModule))]
public sealed class GranitInvoicingWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<IInvoiceCommandPublisher, WolverineInvoiceCommandPublisher>();
}
