using Granit.Invoicing.Endpoints.Endpoints;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Invoicing.Endpoints.Extensions;

/// <summary>Extension methods for registering invoicing endpoints.</summary>
public static class InvoicingEndpointRouteBuilderExtensions
{
    /// <summary>Maps the invoicing administration endpoints.</summary>
    public static RouteGroupBuilder MapGranitInvoicing(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGranitGroup("invoicing")
            .WithTags("Invoicing");

        group.MapInvoiceEndpoints();

        return group;
    }
}
