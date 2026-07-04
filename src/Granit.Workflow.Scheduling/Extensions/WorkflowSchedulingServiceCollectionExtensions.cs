using Granit.Workflow.Scheduling.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Workflow.Scheduling.Extensions;

/// <summary>
/// Extension methods for registering Granit.Workflow.Scheduling services.
/// </summary>
public static class WorkflowSchedulingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scheduled-workflow-transition bridge: <see cref="IScheduledTransitionService"/>
    /// and <see cref="IWorkflowTransitionApplierRegistry"/>. Register an
    /// <see cref="IWorkflowTransitionApplier"/> per entity type with
    /// <see cref="AddWorkflowTransitionApplier{TApplier}"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddWorkflowScheduling(this IServiceCollection services)
    {
        services.TryAddScoped<IScheduledTransitionService, DefaultScheduledTransitionService>();
        services.TryAddScoped<IWorkflowTransitionApplierRegistry, WorkflowTransitionApplierRegistry>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IWorkflowTransitionApplier"/> under the given workflow entity type
    /// key, so scheduled transitions carrying that <c>WorkflowEntityType</c> are dispatched to it.
    /// </summary>
    /// <typeparam name="TApplier">The applier implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="workflowEntityType">
    /// The logical workflow entity type key (matching
    /// <c>ScheduledWorkflowTransitionPayload.WorkflowEntityType</c> and the entity's
    /// <c>IWorkflowStateful.WorkflowEntityType</c>), e.g. <c>"BlogPost"</c>.
    /// </param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddWorkflowTransitionApplier<TApplier>(
        this IServiceCollection services,
        string workflowEntityType)
        where TApplier : class, IWorkflowTransitionApplier
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowEntityType);
        services.AddKeyedScoped<IWorkflowTransitionApplier, TApplier>(workflowEntityType);
        return services;
    }
}
