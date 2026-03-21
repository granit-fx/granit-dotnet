namespace Granit.Identity.Internal;

/// <summary>
/// No-op implementation of <see cref="IIdentityEventPublisher"/>.
/// Registered by default when no message bus integration is configured.
/// </summary>
[Obsolete("Use IDistributedEventBus from Granit.Core.Events instead. Identity events are now IIntegrationEvent records (*Eto suffix).")]
#pragma warning disable CS0618 // Type or member is obsolete
internal sealed class NullIdentityEventPublisher : IIdentityEventPublisher
#pragma warning restore CS0618
{
    /// <inheritdoc/>
    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : notnull =>
        Task.CompletedTask;
}
