using Granit.DocumentGeneration;
using Granit.Invoicing.Internal.Internal;
using Granit.Modularity;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Invoicing.Internal;

/// <summary>
/// Self-hosted invoice document generation via Granit.Templating (Scriban → HTML → PDF).
/// </summary>
[DependsOn(
    typeof(GranitDocumentGenerationModule),
    typeof(GranitInvoicingModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitInvoicingInternalModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddEmbeddedTemplates(typeof(GranitInvoicingInternalModule).Assembly);
        context.Services.TryAddScoped<IInvoiceDocumentGenerator, InternalInvoiceDocumentGenerator>();
    }
}
