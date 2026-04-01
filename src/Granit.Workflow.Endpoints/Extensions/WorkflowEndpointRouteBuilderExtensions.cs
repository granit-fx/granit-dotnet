using Granit.Validation.AspNetCore;
using Granit.Workflow.Endpoints.Endpoints;
using Granit.Workflow.Endpoints.Options;
using Granit.Workflow.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
    /// Requires the <c>Workflow.History.Read</c> permission on the route group.
    /// </para>
    /// <para>Registers the following routes:</para>
    /// <list type="bullet">
    ///   <item><c>GET /{entityType}/{entityId}/history</c> — ISO 27001 audit trail</item>
    /// </list>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitWorkflow();
    ///
    /// // With a custom prefix:
    /// app.MapGranitWorkflow(opts =&gt;
    /// {
    ///     opts.RoutePrefix = "admin/workflow";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize options.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitWorkflow(
        this IEndpointRouteBuilder endpoints,
        Action<WorkflowEndpointsOptions>? configure = null)
    {
        WorkflowEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.RequireAuthorization(WorkflowPermissions.History.Read).MapReadEndpoints();

        return group;
    }

    /// <summary>
    /// Maps workflow transition endpoints (available transitions + execute) for a specific
    /// workflow state type onto the given route group.
    /// </summary>
    /// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
    /// <param name="group">The route group to add endpoints to.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <remarks>
    /// <para>Registers the following routes:</para>
    /// <list type="bullet">
    ///   <item><c>GET /transitions?currentState={state}</c> — available transitions for the current user</item>
    ///   <item><c>POST /transitions?currentState={state}</c> — evaluate and execute a transition</item>
    /// </list>
    /// <para>
    /// These endpoints operate on state names (strings) and do not persist entity state.
    /// The caller is responsible for updating the entity with the resulting state returned
    /// by the <c>POST</c> endpoint.
    /// </para>
    /// <para>
    /// Requires <c>IWorkflowManager&lt;TState&gt;</c> to be registered via
    /// <c>services.AddWorkflow&lt;TState&gt;(definition)</c>.
    /// </para>
    /// <para>Usage:</para>
    /// <code>
    /// var group = app.MapGranitWorkflow();
    /// group.MapGranitWorkflowTransition&lt;DocumentStatus&gt;();
    /// </code>
    /// </remarks>
    public static RouteGroupBuilder MapGranitWorkflowTransition<TState>(
        this RouteGroupBuilder group)
        where TState : struct, Enum
    {
        WorkflowTransitionEndpoints<TState>.MapTransitionEndpoints(group);
        return group;
    }

}
