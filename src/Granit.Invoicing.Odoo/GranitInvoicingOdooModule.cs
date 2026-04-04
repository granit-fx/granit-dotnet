using Granit.Http.Resilience;
using Granit.Http.Resilience.Extensions;
using Granit.Invoicing.Odoo.Internal;
using Granit.Invoicing.Odoo.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Invoicing.Odoo;

/// <summary>Odoo accounting sync for Granit.Invoicing via JSON-RPC.</summary>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitInvoicingModule))]
public sealed class GranitInvoicingOdooModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<OdooOptions>()
            .BindConfiguration(OdooOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.AddGranitHttpClient("Odoo");

        context.Services.AddScoped<OdooJsonRpcClient>();
        context.Services.TryAddScoped<IInvoiceSyncProvider, OdooInvoiceSyncProvider>();
    }
}
