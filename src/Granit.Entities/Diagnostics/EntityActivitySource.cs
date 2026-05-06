using System.Diagnostics;
using Granit.Diagnostics;

namespace Granit.Entities.Diagnostics;

/// <summary>
/// <see cref="ActivitySource"/> for the entity-manifest runtime — emitted by the
/// HTTP endpoints (relations-aggregate span, story #1561) and any future
/// runtime span on this package. Auto-registered with
/// <see cref="GranitActivitySourceRegistry"/> by <c>AddGranitEntities</c> so
/// OpenTelemetry exporters pick it up regardless of whether the host loads
/// the HTTP layer.
/// </summary>
public static class EntityActivitySource
{
    /// <summary>The OpenTelemetry instrumentation name.</summary>
    public const string Name = "Granit.Entities";

    /// <summary>The shared <see cref="ActivitySource"/> instance.</summary>
    public static readonly ActivitySource Source = new(Name);
}
