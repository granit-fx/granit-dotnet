namespace Granit.Features.Events;

/// <summary>
/// No-op implementation of <see cref="IFeatureEventPublisher"/>.
/// Registered by default; replaced when audit log infrastructure is installed.
/// </summary>
internal sealed class NullFeatureEventPublisher : IFeatureEventPublisher
{
    /// <inheritdoc/>
    public Task PublishAsync(FeatureOverrideChangedEvent featureOverrideChangedEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
