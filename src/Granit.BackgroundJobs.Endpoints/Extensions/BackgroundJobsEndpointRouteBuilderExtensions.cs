using Granit.BackgroundJobs.Endpoints.Endpoints;
using Granit.BackgroundJobs.Endpoints.Options;
using Granit.BackgroundJobs.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.BackgroundJobs.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering background jobs administration endpoints.
/// </summary>
public static class BackgroundJobsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the background jobs administration endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read endpoints require the <c>BackgroundJobs.Jobs.Read</c> permission;
    /// write endpoints (pause, resume, trigger) require <c>BackgroundJobs.Jobs.Manage</c>.
    /// </para>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitBackgroundJobs();
    ///
    /// // With a custom prefix:
    /// app.MapGranitBackgroundJobs(opts =>
    /// {
    ///     opts.RoutePrefix = "admin/jobs";
    /// });
    /// </code>
    /// <para>
    /// Exposes 5 endpoints: GET /{prefix}, GET /{prefix}/{name},
    /// POST /{prefix}/{name}/pause, POST /{prefix}/{name}/resume,
    /// POST /{prefix}/{name}/trigger.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="BackgroundJobsEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitBackgroundJobs(
        this IEndpointRouteBuilder endpoints,
        Action<BackgroundJobsEndpointsOptions>? configure = null)
    {
        BackgroundJobsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.RequireAuthorization(BackgroundJobsPermissions.Jobs.Read).MapReadEndpoints();
        group.RequireAuthorization(BackgroundJobsPermissions.Jobs.Manage).MapWriteEndpoints();

        return group;
    }
}
