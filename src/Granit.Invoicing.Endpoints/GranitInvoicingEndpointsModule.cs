using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Invoicing.Endpoints.Internal;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Invoicing.Endpoints;

/// <summary>Granit module for invoicing administration HTTP endpoints.</summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitInvoicingModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitValidationModule))]
public sealed class GranitInvoicingEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISchemaExampleProvider, InvoicingSchemaExampleProvider>());
}
