using System.Reflection;
using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Wolverine.Internal;
using Granit.Modularity;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;
using Wolverine.Runtime.Handlers;

namespace Granit.BackgroundJobs.Wolverine;

/// <summary>
/// Granit module that replaces the default Channel-based background job dispatch with
/// durable Wolverine implementations: <c>IMessageBus</c>-backed dispatcher, cluster-safe
/// <c>SingularAgent</c> scheduler, and atomic rescheduling middleware.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitWolverineModule))]
public sealed class GranitBackgroundJobsWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Replace Channel-based dispatcher with Wolverine IMessageBus.
        context.Services.Replace(ServiceDescriptor
            .Scoped<IBackgroundJobDispatcher, WolverineBackgroundJobDispatcher>());

        // Replace null DLQ inspector with Wolverine IMessageStore.
        context.Services.Replace(ServiceDescriptor
            .Scoped<IDeadLetterQueueInspector, WolverineDeadLetterQueueInspector>());

        // Cluster-safe singleton scheduler (replaces in-process ChannelCronSchedulerService).
        context.Services.AddSingularAgent<CronSchedulerAgent>();

        // Register Wolverine middleware for atomic rescheduling on recurring job handlers.
        context.Services.ConfigureWolverine(opts =>
        {
            opts.Policies.AddMiddleware<RecurringJobSchedulingMiddleware>(
                (HandlerChain chain) => chain.MessageType
                    .GetCustomAttribute<RecurringJobAttribute>() is not null);
        });
    }
}
