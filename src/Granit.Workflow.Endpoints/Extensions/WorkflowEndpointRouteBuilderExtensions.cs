using Granit.Validation.AspNetCore;
using Granit.Workflow.Endpoints.Endpoints;
using Granit.Workflow.Endpoints.Internal;
using Granit.Workflow.Endpoints.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Workflow.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering workflow endpoints.
/// </summary>
public static class WorkflowEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the workflow endpoints (transition history, status) onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers the <c>Workflow.History</c> authorization policy (see
    /// <see cref="WorkflowAuthorizationPolicy.PolicyName"/>) requiring the role
    /// configured via <see cref="WorkflowEndpointsOptions.RequiredRole"/>.
    /// </para>
    /// <para>Registers the following routes:</para>
    /// <list type="bullet">
    ///   <item><c>GET /{entityType}/{entityId}/history</c> — ISO 27001 audit trail</item>
    /// </list>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapWorkflowEndpoints();
    ///
    /// // With a custom prefix or role:
    /// app.MapWorkflowEndpoints(opts =&gt;
    /// {
    ///     opts.RoutePrefix = "admin/workflow";
    ///     opts.RequiredRole = "ops-team";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize options.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapWorkflowEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<WorkflowEndpointsOptions>? configure = null)
    {
        WorkflowEndpointsOptions options = new();
        configure?.Invoke(options);

        IOptions<AuthorizationOptions>? authOptions =
            endpoints.ServiceProvider.GetService<IOptions<AuthorizationOptions>>();
        authOptions?.Value.AddPolicy(
            WorkflowAuthorizationPolicy.PolicyName,
            policy => policy.RequireRole(options.RequiredRole));

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(WorkflowAuthorizationPolicy.PolicyName);

        group.MapReadEndpoints();

        return group;
    }
}
