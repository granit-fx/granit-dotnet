using Granit.Invoicing.Domain;
using Granit.Invoicing.Endpoints.Endpoints;
using Granit.Invoicing.Endpoints.Options;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Invoicing.Endpoints.Extensions;

/// <summary>Extension methods for registering invoicing endpoints.</summary>
public static class InvoicingEndpointRouteBuilderExtensions
{
    /// <summary>Maps the invoicing administration endpoints.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="InvoicingEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitInvoicing(
        this IEndpointRouteBuilder endpoints,
        Action<InvoicingEndpointsOptions>? configure = null)
    {
        InvoicingEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.MapInvoiceEndpoints();

        // Query engine endpoint — paginated, filterable list.
        // When no tenant context is active, the IQueryableSource disables the
        // multi-tenant filter so host admin sees all invoices cross-tenant.
        group.MapGranitGroup("invoices").MapGranitQuery<Invoice>();

        return group;
    }
}
