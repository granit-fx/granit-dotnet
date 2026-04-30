using Granit.Authorization;
using Granit.Entities;
using Granit.Entities.Relations;
using Granit.Http.ApiDocumentation;
using Granit.Invoicing.Endpoints.Internal;
using Granit.Invoicing.Endpoints.Relations;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Invoicing.Endpoints;

/// <summary>Granit module for invoicing administration HTTP endpoints.</summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitEntitiesAbstractionsModule),
    typeof(GranitInvoicingModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitValidationModule))]
public sealed class GranitInvoicingEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISchemaExampleProvider, InvoicingSchemaExampleProvider>());

        // Phase 1.F-β cobaye: graft the "invoices" smart-button relation onto Party.
        context.Services.AddEntityRelationContribution<InvoicesOnPartyRelationContribution>();
    }
}
