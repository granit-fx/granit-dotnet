using Granit.Modularity;
using Granit.Scheduling.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Runtime.Handlers;

namespace Granit.Scheduling.Wolverine;

/// <summary>
/// Granit module that provides durable Wolverine-backed scheduling for one-shot actions.
/// Registers <see cref="WolverineScheduler"/> as the <see cref="IScheduler"/> implementation
/// and applies <see cref="ScheduledActionStatusMiddleware"/> to all <see cref="IScheduledPayload"/>
/// handlers.
/// </summary>
[DependsOn(
    typeof(GranitSchedulingModule),
    typeof(GranitWolverineModule))]
public sealed class GranitSchedulingWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<IScheduler, WolverineScheduler>();

        context.Services.ConfigureWolverine(opts =>
        {
            opts.Policies.AddMiddleware<ScheduledActionStatusMiddleware>(
                (HandlerChain chain) => typeof(IScheduledPayload).IsAssignableFrom(chain.MessageType));
        });
    }
}
