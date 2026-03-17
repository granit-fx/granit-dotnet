namespace Granit.Features.Events;

/// <summary>
/// Publishes <see cref="FeatureOverrideChangedEvent"/> after feature override mutations.
/// </summary>
/// <remarks>
/// Default implementation is <see cref="NullFeatureEventPublisher"/> (no-op).
/// Replace with a Wolverine-backed publisher for audit log integration.
/// </remarks>
public interface IFeatureEventPublisher
{
    /// <summary>
    /// Publishes a feature override change event.
    /// </summary>
    Task PublishAsync(FeatureOverrideChangedEvent featureOverrideChangedEvent, CancellationToken cancellationToken = default);
}
