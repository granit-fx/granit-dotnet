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
    /// <see cref="AddWorkflow{TState}(IServiceCollection, IWorkflowDefinition{TState})"/>
    /// (single-entity) or its keyed overload (several entities sharing a state enum).
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
        services.TryAddScoped<IWorkflowManagerFactory, WorkflowManagerFactory>();

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
    /// <remarks>
    /// <para>
    /// Use this for a module with a single entity per <typeparamref name="TState"/>: inject
    /// <see cref="IWorkflowManager{TState}"/> directly. When two entities share the same
    /// <typeparamref name="TState"/> but need distinct, permission-gated definitions, use the
    /// keyed overload <see cref="AddWorkflow{TState}(IServiceCollection, string, IWorkflowDefinition{TState})"/>
    /// and resolve via <see cref="IWorkflowManagerFactory"/>.
    /// </para>
    /// <para>
    /// The definition is also registered under a default key (<c>typeof(TState).FullName</c>)
    /// so it is reachable through <see cref="IWorkflowManagerFactory"/> alongside keyed workflows.
    /// </para>
    /// </remarks>
    /// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="definition">The immutable workflow definition.</param>
    public static IServiceCollection AddWorkflow<TState>(
        this IServiceCollection services,
        IWorkflowDefinition<TState> definition)
        where TState : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(definition);

        // Non-keyed registrations preserve the single-entity ergonomics:
        // inject IWorkflowDefinition<TState> / IWorkflowManager<TState> directly.
        services.AddSingleton(definition);
        services.AddScoped<IWorkflowManager<TState>, WorkflowManager<TState>>();

        // Also expose it through the keyed seam under a default key so the factory can
        // resolve single-entity workflows uniformly with keyed ones.
        return services.AddWorkflow(DefaultWorkflowKey<TState>(), definition);
    }

    /// <summary>
    /// Registers a workflow definition and its <see cref="IWorkflowManager{TState}"/>
    /// keyed by <paramref name="workflowEntityType"/>, allowing several entities to share the same
    /// <typeparamref name="TState"/> enum with distinct definitions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolve the resulting manager through <see cref="IWorkflowManagerFactory.GetManager{TState}(string)"/>
    /// (or <see cref="IWorkflowManagerFactory.GetManager{TEntity, TState}()"/>), or by injecting
    /// <c>[FromKeyedServices(workflowEntityType)] IWorkflowManager&lt;TState&gt;</c>.
    /// </para>
    /// <para>
    /// <paramref name="workflowEntityType"/> should match the entity's
    /// <see cref="Domain.IWorkflowStateful.WorkflowEntityType"/> (e.g. <c>"BlogPost"</c>).
    /// </para>
    /// </remarks>
    /// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="workflowEntityType">The logical entity type key.</param>
    /// <param name="definition">The immutable workflow definition.</param>
    /// <exception cref="ArgumentException"><paramref name="workflowEntityType"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    public static IServiceCollection AddWorkflow<TState>(
        this IServiceCollection services,
        string workflowEntityType,
        IWorkflowDefinition<TState> definition)
        where TState : struct, Enum
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowEntityType);
        ArgumentNullException.ThrowIfNull(definition);

        services.AddKeyedSingleton(workflowEntityType, definition);
        services.AddKeyedScoped<IWorkflowManager<TState>>(workflowEntityType, static (sp, key) =>
            ActivatorUtilities.CreateInstance<WorkflowManager<TState>>(
                sp, sp.GetRequiredKeyedService<IWorkflowDefinition<TState>>(key)));
        return services;
    }

    /// <summary>
    /// The default key under which a non-keyed workflow definition is also registered,
    /// so it remains reachable through <see cref="IWorkflowManagerFactory"/>.
    /// </summary>
    private static string DefaultWorkflowKey<TState>()
        where TState : struct, Enum
        => typeof(TState).FullName!;
}
