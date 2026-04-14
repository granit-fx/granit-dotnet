using Granit.DocumentGeneration;
using Granit.Invoicing.Builtin.Internal;
using Granit.Modularity;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Invoicing.Builtin;

/// <summary>
/// Self-hosted invoice document generation via Granit.Templating (Scriban → HTML → PDF).
/// </summary>
[DependsOn(
    typeof(GranitDocumentGenerationModule),
    typeof(GranitInvoicingModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitInvoicingBuiltinModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddEmbeddedTemplates(typeof(GranitInvoicingBuiltinModule).Assembly);
        context.Services.TryAddScoped<IInvoiceDocumentGenerator, BuiltinInvoiceDocumentGenerator>();
    }
}
