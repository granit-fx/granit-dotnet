using Granit.Core.Modularity;
using Granit.EventBus.Extensions;

namespace Granit.EventBus;

/// <summary>
/// Granit module for the default in-process event bus.
/// </summary>
/// <remarks>
/// Provides <c>InProcessLocalEventBus</c> and <c>InProcessDistributedEventBus</c>
/// as default implementations. Replace with <c>Granit.EventBus.Wolverine</c> for
/// durable, outbox-backed delivery in production.
/// </remarks>
public sealed class GranitEventBusModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitEventBus();
}
