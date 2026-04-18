using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.QueryEngine.Extensions;
using Granit.Workflow.Diagnostics;
using Granit.Workflow.Domain;
using Granit.Workflow.Exports;
using Granit.Workflow.Internal;
using Granit.Workflow.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Workflow.Extensions;

/// <summary>
/// Extension methods for registering Granit.Workflow services.
/// </summary>
public static class WorkflowServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Granit.Workflow core services:
    /// <list type="bullet">
    ///   <item><see cref="IWorkflowPermissionChecker"/> (null-object default, replaceable by Granit.Authorization bridge)</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// To register a specific workflow definition and manager, use
    /// <see cref="AddWorkflow{TState}"/>.
    /// </para>
    /// <para>
    /// The <see cref="IWorkflowPermissionChecker"/> default always grants permissions.
    /// When <c>Granit.Authorization</c> is registered, replace it with a bridge
    /// that delegates to <c>IPermissionChecker</c>.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGranitWorkflow(this IServiceCollection services)
    {
        services.TryAddScoped<IWorkflowPermissionChecker, NullWorkflowPermissionChecker>();

        // Diagnostics
        services.TryAddSingleton<WorkflowMetrics>();
        GranitActivitySourceRegistry.Register(WorkflowActivitySource.Name);

        // Query + Export definitions (ADR-020: owned by the base module).
        services.AddQueryDefinition<WorkflowTransitionRecord, WorkflowTransitionRecordQueryDefinition>();
        services.AddExportDefinition<WorkflowTransitionRecord, WorkflowTransitionRecordExportDefinition>();

        return services;
    }

    /// <summary>
    /// Registers a workflow definition and its <see cref="IWorkflowManager{TState}"/>
    /// for the specified state type.
    /// </summary>
    /// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="definition">The immutable workflow definition.</param>
    public static IServiceCollection AddWorkflow<TState>(
        this IServiceCollection services,
        IWorkflowDefinition<TState> definition)
        where TState : struct, Enum
    {
        services.AddSingleton(definition);
        services.AddScoped<IWorkflowManager<TState>, WorkflowManager<TState>>();
        return services;
    }
}
