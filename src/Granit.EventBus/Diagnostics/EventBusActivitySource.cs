using System.Diagnostics;

namespace Granit.EventBus.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.EventBus distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class EventBusActivitySource
{
    /// <summary>The name of the Granit.EventBus <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.EventBus";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string PublishLocal = "eventbus.publish.local";
    internal const string PublishDistributed = "eventbus.publish.distributed";
}
