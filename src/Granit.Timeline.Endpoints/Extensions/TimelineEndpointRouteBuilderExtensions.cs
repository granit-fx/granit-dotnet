using Granit.Timeline.Endpoints.Endpoints;
using Granit.Timeline.Endpoints.Internal;
using Granit.Timeline.Endpoints.Options;
using Granit.Timeline.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Timeline.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering timeline endpoints.
/// </summary>
public static class TimelineEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the timeline endpoints (stream, entries, followers) onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers the <c>Timeline.Entries.Read</c> authorization policy (see
    /// <see cref="TimelineAuthorizationPolicy.PolicyName"/>) requiring the role
    /// configured via <see cref="TimelineEndpointsOptions.RequiredRole"/>.
    /// </para>
    /// <para>Registers the following routes:</para>
    /// <list type="bullet">
    ///   <item><c>GET /{entityType}/{entityId}</c> — paginated activity stream</item>
    ///   <item><c>POST /{entityType}/{entityId}/entries</c> — post comment/note</item>
    ///   <item><c>DELETE /{entityType}/{entityId}/entries/{id}</c> — soft-delete (RGPD)</item>
    ///   <item><c>POST /{entityType}/{entityId}/follow</c> — follow entity</item>
    ///   <item><c>DELETE /{entityType}/{entityId}/follow</c> — unfollow entity</item>
    ///   <item><c>GET /{entityType}/{entityId}/followers</c> — list followers</item>
    /// </list>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapTimelineEndpoints();
    ///
    /// // With a custom prefix or role:
    /// app.MapTimelineEndpoints(opts =&gt;
    /// {
    ///     opts.RoutePrefix = "admin/timeline";
    ///     opts.RequiredRole = "ops-team";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize options.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapTimelineEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<TimelineEndpointsOptions>? configure = null)
    {
        TimelineEndpointsOptions options = new();
        configure?.Invoke(options);

        IOptions<AuthorizationOptions> authOptions =
            endpoints.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>();
        authOptions.Value.AddPolicy(
            TimelineAuthorizationPolicy.PolicyName,
            policy => policy.RequireRole(options.RequiredRole));
        authOptions.Value.AddPolicy(
            TimelinePermissions.Entries.Create,
            policy => policy.RequireRole(options.RequiredRole));

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(TimelineAuthorizationPolicy.PolicyName);

        group.MapStreamEndpoints();
        group.MapEntryEndpoints();
        group.MapFollowerEndpoints();

        return group;
    }
}
