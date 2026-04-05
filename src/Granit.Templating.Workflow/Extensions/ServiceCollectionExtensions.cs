using Granit.Templating.Store;
using Granit.Templating.Workflow.Internal;
using Granit.Workflow.Definitions;
using Granit.Workflow.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Templating.Workflow.Extensions;

/// <summary>
/// Extension methods for registering the Granit.Templating.Workflow bridge.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default <see cref="NullTemplateTransitionHook"/> with a Workflow-aware
    /// implementation that provides FSM validation and approval routing.
    /// </summary>
    public static IServiceCollection AddGranitTemplatingWorkflow(
        this IServiceCollection services)
    {
        services.AddGranitWorkflow();
        services.AddWorkflow(PublicationWorkflow.Default);
        services.Replace(ServiceDescriptor.Scoped<ITemplateTransitionHook, WorkflowTemplateTransitionHook>());
        return services;
    }
}
