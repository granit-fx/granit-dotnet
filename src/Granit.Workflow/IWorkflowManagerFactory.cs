using Granit.Workflow.Domain;

namespace Granit.Workflow;

/// <summary>
/// Resolves the <see cref="IWorkflowManager{TState}"/> for a specific workflow entity type.
/// </summary>
/// <remarks>
/// <para>
/// Use this when several entities share the same state enum but require distinct,
/// permission-gated <see cref="IWorkflowDefinition{TState}"/> instances (registered via the keyed
/// <c>AddWorkflow&lt;TState&gt;(string workflowEntityType, …)</c> overload). Each keyed definition
/// is served by its own manager, keyed on the entity's
/// <see cref="IWorkflowStateful.WorkflowEntityType"/>.
/// </para>
/// <para>
/// For single-entity modules that register a workflow through the non-keyed
/// <c>AddWorkflow&lt;TState&gt;(definition)</c> overload, inject
/// <see cref="IWorkflowManager{TState}"/> directly — the factory remains available for those too,
/// keyed on <c>typeof(TState).FullName</c>.
/// </para>
/// </remarks>
public interface IWorkflowManagerFactory
{
    /// <summary>
    /// Resolves the <see cref="IWorkflowManager{TState}"/> registered for the given
    /// <paramref name="workflowEntityType"/>.
    /// </summary>
    /// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
    /// <param name="workflowEntityType">
    /// The logical entity type key, matching <see cref="IWorkflowStateful.WorkflowEntityType"/>
    /// (e.g. <c>"BlogPost"</c>) used at registration time.
    /// </param>
    /// <returns>The manager backed by the definition keyed under <paramref name="workflowEntityType"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="workflowEntityType"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// No workflow was registered for <paramref name="workflowEntityType"/> and
    /// <typeparamref name="TState"/>.
    /// </exception>
    IWorkflowManager<TState> GetManager<TState>(string workflowEntityType)
        where TState : struct, Enum;

    /// <summary>
    /// Resolves the <see cref="IWorkflowManager{TState}"/> for the entity type
    /// <typeparamref name="TEntity"/>, using its static
    /// <see cref="IWorkflowStateful.WorkflowEntityType"/> as the key.
    /// </summary>
    /// <typeparam name="TEntity">The workflow-stateful entity type.</typeparam>
    /// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
    /// <returns>The manager backed by the definition keyed for <typeparamref name="TEntity"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// No workflow was registered for <typeparamref name="TEntity"/> and
    /// <typeparamref name="TState"/>.
    /// </exception>
    IWorkflowManager<TState> GetManager<TEntity, TState>()
        where TEntity : IWorkflowStateful
        where TState : struct, Enum;
}
