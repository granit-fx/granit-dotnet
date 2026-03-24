using System.Diagnostics;

namespace Granit.Events.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Events distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class EventsActivitySource
{
    /// <summary>The name of the Granit.Events <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Events";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string PublishLocal = "events.publish.local";
    internal const string PublishDistributed = "events.publish.distributed";
}
