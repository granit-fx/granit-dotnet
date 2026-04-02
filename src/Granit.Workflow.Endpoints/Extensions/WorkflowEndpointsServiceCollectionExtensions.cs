using Granit.Authorization;
using Granit.Workflow.Endpoints.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Workflow.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering Granit.Workflow.Endpoints services.
/// </summary>
public static class WorkflowEndpointsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the workflow endpoints services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the default <c>NullWorkflowPermissionChecker</c> (always-grant) with
    /// <see cref="AuthorizationWorkflowPermissionChecker"/>, which delegates to
    /// <c>IPermissionChecker</c> from <c>Granit.Authorization</c>.
    /// </para>
    /// <para>
    /// <see cref="IWorkflowHistoryQuery"/> must be registered separately,
    /// typically via <c>AddGranitWorkflowEntityFrameworkCore&lt;TDbContext&gt;()</c>
    /// from the <c>Granit.Workflow.EntityFrameworkCore</c> package.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitWorkflowEndpoints(
        this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IWorkflowPermissionChecker, AuthorizationWorkflowPermissionChecker>());
        return services;
    }
}
