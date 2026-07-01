using Granit.Auditing.Domain;
using Granit.Auditing.Endpoints.Endpoints;
using Granit.Auditing.Endpoints.Options;
using Granit.Auditing.Endpoints.Permissions;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Auditing.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering audit log API endpoints.
/// </summary>
public static class AuditingEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps read-only audit log endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AuditingEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <remarks>
    /// <para>All endpoints are protected by the <c>Auditing.AuditEntries.Read</c> permission.</para>
    /// <para>Call from your application:</para>
    /// <code>
    /// app.MapGranitAuditing();
    /// </code>
    /// </remarks>
    public static RouteGroupBuilder MapGranitAuditing(
        this IEndpointRouteBuilder endpoints,
        Action<AuditingEndpointsOptions>? configure = null)
    {
        AuditingEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(AuditingPermissions.AuditEntries.Read);

        // Audit entries: query engine (list/meta/saved-views) + custom lookups + GDPR ops.
        RouteGroupBuilder entriesGroup = group.MapGranitGroup("audit-entries");
        // Cross-tenant reads are fail-closed by default: a host operator with no resolved tenant
        // sees only the host partition. For ISO 27001 cross-tenant audit review, mark this route
        // .AllowHostAccess(); a platform admin holding AuditEntries.Read at global scope then reads
        // across tenants, while the multi-tenant filter stays enforced for every tenant-scoped caller.
        entriesGroup.MapGranitQuery<AuditEntry>();
        entriesGroup.MapAuditingReadEndpoints();
        entriesGroup.MapAuditingManagementEndpoints();

        // Audit entity changes: query engine only (cross-cutting analysis).
        // Detail of an entity change is reached via the parent AuditEntry detail endpoint.
        // Fail-closed to the host partition by default; cross-tenant review is opt-in via
        // .AllowHostAccess() plus a global-scoped AuditEntries.Read grant.
        group.MapGranitGroup("audit-entity-changes").MapGranitQuery<AuditEntityChange>();

        return group;
    }
}
