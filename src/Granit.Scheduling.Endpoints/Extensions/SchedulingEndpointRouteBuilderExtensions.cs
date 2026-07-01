using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Endpoints.Endpoints;
using Granit.Scheduling.Endpoints.Options;
using Granit.Scheduling.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Scheduling.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering scheduled actions administration endpoints.
/// </summary>
public static class SchedulingEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the scheduled actions administration endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read endpoints require the <c>Scheduling.Actions.Read</c> permission;
    /// write endpoints (cancel, reschedule) require <c>Scheduling.Actions.Manage</c>.
    /// The list/search endpoint uses <c>MapGranitQuery</c> for pagination, filtering, and sorting.
    /// </para>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitScheduling();
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="SchedulingEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitScheduling(
        this IEndpointRouteBuilder endpoints,
        Action<SchedulingEndpointsOptions>? configure = null)
    {
        SchedulingEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        RouteGroupBuilder actionsGroup = group.MapGranitGroup("scheduled-actions");

        actionsGroup.RequireAuthorization(SchedulingPermissions.Actions.Read).MapReadEndpoints();
        actionsGroup.RequireAuthorization(SchedulingPermissions.Actions.Manage).MapWriteEndpoints();

        // QueryEngine-powered list endpoint with pagination, filtering, and sorting
        // Cross-tenant reads are fail-closed by default: a host operator with no resolved tenant
        // sees only the host partition. To expose cross-tenant scheduled-action visibility, mark this
        // route .AllowHostAccess(); a platform admin holding Actions.Read at global scope then reads
        // across tenants, while the multi-tenant filter stays enforced for every tenant-scoped caller.
        actionsGroup.RequireAuthorization(SchedulingPermissions.Actions.Read)
            .MapGranitQuery<ScheduledAction>();

        return group;
    }
}
