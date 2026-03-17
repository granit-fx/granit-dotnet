using Granit.Core.Events;
using Granit.Core.Modularity;
using Granit.EventBus.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.EventBus.Wolverine;

/// <summary>
/// Wolverine-backed event bus module. Replaces the in-process defaults from
/// <see cref="GranitEventBusModule"/> with Wolverine-backed providers.
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
    typeof(GranitEventBusModule),
    typeof(GranitWolverineModule))]
public sealed class GranitEventBusWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<ILocalEventBus, WolverineLocalEventBus>();
        context.Services.AddScoped<IDistributedEventBus, WolverineDistributedEventBus>();
    }
}
