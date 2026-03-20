using Granit.BackgroundJobs.Endpoints.Endpoints;
using Granit.BackgroundJobs.Endpoints.Internal;
using Granit.BackgroundJobs.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    /// Registers the <c>BackgroundJobs.Jobs.Manage</c> authorization policy (see
    /// <see cref="BackgroundJobsAuthorizationPolicy.PolicyName"/>) requiring the role
    /// configured via <see cref="BackgroundJobsEndpointsOptions.RequiredRole"/>.
    /// </para>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapBackgroundJobsEndpoints();
    ///
    /// // With a custom prefix or role:
    /// app.MapBackgroundJobsEndpoints(opts =>
    /// {
    ///     opts.RoutePrefix = "admin/jobs";
    ///     opts.RequiredRole = "ops-team";
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
    public static RouteGroupBuilder MapBackgroundJobsEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<BackgroundJobsEndpointsOptions>? configure = null)
    {
        BackgroundJobsEndpointsOptions options = new();
        configure?.Invoke(options);

        // Register the named authorization policy so that endpoints can use
        // RequireAuthorization(PolicyName). This is safe to call here because
        // IOptions<AuthorizationOptions> is a singleton and is evaluated lazily
        // (before the first policy lookup at request time).
        IOptions<AuthorizationOptions> authOptions =
            endpoints.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>();
        authOptions.Value.AddPolicy(
            BackgroundJobsAuthorizationPolicy.PolicyName,
            policy => policy.RequireRole(options.RequiredRole));

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(BackgroundJobsAuthorizationPolicy.PolicyName);

        group.MapReadEndpoints();
        group.MapWriteEndpoints();

        return group;
    }
}
