using System.Diagnostics;
using Granit.Diagnostics;

namespace Granit.Entities.Endpoints.Diagnostics;

/// <summary>
/// <see cref="ActivitySource"/> for the entity-manifest HTTP surface — used
/// today for the relations-aggregate endpoint span (story #1561), available
/// as the catch-all for future spans on this package. Auto-registered with
/// <see cref="GranitActivitySourceRegistry"/> so OpenTelemetry exporters
/// pick it up when the host wires Granit observability.
/// </summary>
internal static class EntityActivitySource
{
    public const string Name = "Granit.Entities.Endpoints";

    public static readonly ActivitySource Source = new(Name);
}
