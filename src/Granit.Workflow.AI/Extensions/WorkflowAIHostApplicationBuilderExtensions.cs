using System.Diagnostics.CodeAnalysis;
using Granit.Workflow.AI.Internal;
using Granit.Workflow.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Workflow.AI.Extensions;

/// <summary>
/// Extension methods for registering Granit Workflow AI services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class WorkflowAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Granit Workflow AI services and binds <see cref="WorkflowAIOptions"/>
    /// from the <c>Workflow:AI</c> configuration section.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitWorkflowAI(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<WorkflowAIOptions>()
            .BindConfiguration(WorkflowAIOptions.SectionName);

        builder.Services.TryAddScoped<IAITransitionAdvisor, LlmTransitionAdvisor>();
        builder.Services.TryAddScoped<IAIApprovalEvaluator, LlmApprovalEvaluator>();

        return builder;
    }
}
