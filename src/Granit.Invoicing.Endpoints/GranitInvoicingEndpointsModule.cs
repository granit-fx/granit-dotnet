using Granit.Authorization;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Endpoints.Queries;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.QueryEngine.Extensions;
using Granit.Validation;

namespace Granit.Invoicing.Endpoints;

/// <summary>Granit module for invoicing administration HTTP endpoints.</summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitInvoicingModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitValidationModule))]
public sealed class GranitInvoicingEndpointsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddQueryDefinition<Invoice, InvoiceQueryDefinition>();
    }
}
