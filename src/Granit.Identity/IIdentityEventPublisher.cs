using Granit.Core.Events;

namespace Granit.Identity;

/// <summary>
/// Publishes identity domain events after successful write operations on the identity provider.
/// </summary>
/// <remarks>
/// <para>
/// This interface is obsolete. Identity providers now use <see cref="IDistributedEventBus"/>
/// directly to publish integration events (<c>*Eto</c> records implementing <see cref="IIntegrationEvent"/>).
/// </para>
/// </remarks>
[Obsolete("Use IDistributedEventBus from Granit.Core.Events instead. Identity events are now IIntegrationEvent records (*Eto suffix).")]
public interface IIdentityEventPublisher
{
    /// <summary>
    /// Publishes an identity domain event.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="domainEvent">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
