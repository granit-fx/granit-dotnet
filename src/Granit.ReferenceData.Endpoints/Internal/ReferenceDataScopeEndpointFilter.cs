using Granit.MultiTenancy;
using Granit.ReferenceData.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.ReferenceData.Endpoints.Internal;

/// <summary>
/// Endpoint filter that enforces scope-based access control on reference data admin endpoints.
/// <list type="bullet">
/// <item><description><see cref="ReferenceDataScope.Global"/>: rejects requests from a tenant context (403).</description></item>
/// <item><description><see cref="ReferenceDataScope.Tenant"/>: rejects requests from the host context (403).</description></item>
/// </list>
/// </summary>
internal sealed class ReferenceDataScopeEndpointFilter(ReferenceDataScope scope) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ICurrentTenant currentTenant = context.HttpContext.RequestServices.GetRequiredService<ICurrentTenant>();

        return scope switch
        {
            ReferenceDataScope.Global when currentTenant.IsAvailable =>
                new ValueTask<object?>(TypedResults.Problem(
                    detail: "Global reference data can only be managed from the host context.",
                    statusCode: StatusCodes.Status403Forbidden)),

            ReferenceDataScope.Tenant when !currentTenant.IsAvailable =>
                new ValueTask<object?>(TypedResults.Problem(
                    detail: "Tenant reference data can only be managed from a tenant context.",
                    statusCode: StatusCodes.Status403Forbidden)),

            _ => next(context),
        };
    }
}
