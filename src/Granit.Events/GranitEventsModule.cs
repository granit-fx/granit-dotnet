using Granit.Events.Extensions;
using Granit.Modularity;

namespace Granit.Events;

/// <summary>
/// Granit module for the default in-process event bus.
/// </summary>
/// <remarks>
/// Provides <c>InProcessLocalEventBus</c> and <c>InProcessDistributedEventBus</c>
/// as default implementations. Replace with <c>Granit.Events.Wolverine</c> for
/// durable, outbox-backed delivery in production.
/// </remarks>
public sealed class GranitEventsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitEvents();
}
