using Granit.Events;
using Granit.Events.Wolverine.Internal;
using Granit.Modularity;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Events.Wolverine;

/// <summary>
/// Wolverine-backed event bus module. Replaces the in-process defaults from
/// <see cref="GranitEventsModule"/> with Wolverine-backed providers.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WolverineLocalEventBus"/>: publishes to Wolverine local queue.
/// <see cref="WolverineDistributedEventBus"/>: publishes via <c>IMessageBus</c> with outbox.
/// </para>
/// <para>
/// Requires <c>Granit.Wolverine</c> for context propagation and handler discovery,
/// plus a transport provider (<c>Granit.Wolverine.Postgresql</c> or <c>.SqlServer</c>)
/// for outbox durability.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitEventsModule),
    typeof(GranitWolverineModule))]
public sealed class GranitEventsWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Host readiness indicator — enables graceful fallback before Wolverine starts.
        // Registered as singleton + IHostedLifecycleService so StartedAsync fires after
        // all IHostedService.StartAsync calls (including Wolverine's) have completed.
        context.Services.AddSingleton<WolverineHostReadiness>();
        context.Services.AddHostedService(sp => sp.GetRequiredService<WolverineHostReadiness>());

        context.Services.AddScoped<ILocalEventBus, WolverineLocalEventBus>();
        context.Services.AddScoped<IDistributedEventBus, WolverineDistributedEventBus>();
        context.Services.AddScoped<IDomainEventDispatcher, WolverineDomainEventDispatcher>();
        context.Services.AddScoped<IIntegrationEventDispatcher, WolverineIntegrationEventDispatcher>();
    }
}
