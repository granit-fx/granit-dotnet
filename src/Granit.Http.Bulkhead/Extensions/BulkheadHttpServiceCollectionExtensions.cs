using Granit.Http.Bulkhead.Exceptions;
using Granit.Http.ExceptionHandling;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Http.Bulkhead.Extensions;

/// <summary>
/// Extension methods for registering the ASP.NET Core binding of Granit bulkhead isolation.
/// </summary>
public static class BulkheadHttpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HTTP-specific bulkhead services: the RFC 7807 mapping of
    /// <see cref="Granit.Bulkhead.Exceptions.BulkheadRejectedException"/> to HTTP 503.
    /// </summary>
    /// <remarks>
    /// Call <c>AddGranitBulkhead()</c> (from <c>Granit.Bulkhead</c>) for the core registry
    /// and <see cref="Granit.Bulkhead.TenantPartitionedBulkhead"/>. The Granit module system
    /// wires both automatically via <see cref="GranitHttpBulkheadModule"/>.
    /// </remarks>
    public static IServiceCollection AddGranitHttpBulkhead(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IExceptionStatusCodeMapper, BulkheadExceptionStatusCodeMapper>();

        return services;
    }
}
