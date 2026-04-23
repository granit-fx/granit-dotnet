using Granit.Timeline.Endpoints.Endpoints;
using Granit.Timeline.Endpoints.Options;
using Granit.Timeline.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
    /// Requires the <c>Timeline.Entries.Read</c> permission on the route group.
    /// Individual endpoints may require additional permissions (e.g. <c>Timeline.Entries.Create</c>).
    /// </para>
    /// <para>Registers the following routes:</para>
    /// <list type="bullet">
    ///   <item><c>GET /{entityType}/{entityId}</c> — paginated activity stream</item>
    ///   <item><c>POST /{entityType}/{entityId}/entries</c> — post comment/note</item>
    ///   <item><c>DELETE /{entityType}/{entityId}/entries/{id}</c> — soft-delete (GDPR)</item>
    ///   <item><c>POST /{entityType}/{entityId}/follow</c> — follow entity</item>
    ///   <item><c>DELETE /{entityType}/{entityId}/follow</c> — unfollow entity</item>
    ///   <item><c>GET /{entityType}/{entityId}/followers</c> — list followers</item>
    /// </list>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitTimeline();
    ///
    /// // With a custom prefix:
    /// app.MapGranitTimeline(opts =&gt;
    /// {
    ///     opts.RoutePrefix = "admin/timeline";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize options.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitTimeline(
        this IEndpointRouteBuilder endpoints,
        Action<TimelineEndpointsOptions>? configure = null)
    {
        TimelineEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(TimelinePermissions.Entries.Read);

        group.MapStreamEndpoints();
        group.MapEntryEndpoints();
        group.MapFollowerEndpoints();

        return group;
    }

}
