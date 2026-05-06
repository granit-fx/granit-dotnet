using System.Diagnostics.CodeAnalysis;

namespace Granit.Activities;

/// <summary>
/// Read-only view over the catalog of <see cref="ActivityType"/> the framework
/// recognises (ADR-046 §4). Aggregates every registered
/// <see cref="IActivityTypeProvider"/>'s contributions at construction; the
/// <c>Granit.Activities</c> runtime module (story A2) supplies the concrete
/// implementation.
/// </summary>
public interface IActivityRegistry
{
    /// <summary>
    /// Every recognised activity type, keyed by <see cref="ActivityType.Name"/>.
    /// Iteration order is stable across calls but not guaranteed to match
    /// registration order.
    /// </summary>
    IReadOnlyDictionary<string, ActivityType> All { get; }

    /// <summary>
    /// Looks up an activity type by name. Returns <see langword="false"/> when
    /// no provider contributed a type with the given name — typically because
    /// the contributing module is not loaded.
    /// </summary>
    bool TryGet(string name, [NotNullWhen(true)] out ActivityType? type);
}
