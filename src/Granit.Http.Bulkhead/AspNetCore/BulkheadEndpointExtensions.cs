using Granit.Http.Bulkhead.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Http.Bulkhead.AspNetCore;

/// <summary>
/// Extension methods for applying bulkhead isolation to ASP.NET Core endpoints.
/// </summary>
public static class BulkheadEndpointExtensions
{
    /// <summary>
    /// Applies the specified bulkhead isolation policy to the endpoint.
    /// Limits concurrent requests per tenant, returning 503 Service Unavailable when the bulkhead is full.
    /// </summary>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="policyName">Name of the bulkhead policy defined in <c>Bulkhead:Policies</c>.</param>
    public static TBuilder RequireGranitBulkhead<TBuilder>(this TBuilder builder, string policyName)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(policyName);

        return builder.AddEndpointFilter(async (context, next) =>
        {
            TenantPartitionedBulkhead bulkhead = context.HttpContext.RequestServices
                .GetRequiredService<TenantPartitionedBulkhead>();

            BulkheadLease lease = await bulkhead.AcquireAsync(policyName, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            try
            {
                return await next(context).ConfigureAwait(false);
            }
            finally
            {
                lease.Dispose();
            }
        });
    }
}
