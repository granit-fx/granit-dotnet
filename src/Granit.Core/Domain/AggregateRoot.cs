using Granit.Core.Events;

namespace Granit.Core.Domain;

/// <summary>
/// Base class for aggregate roots — entities that form the root of a consistency boundary.
/// Carries domain events (local, dispatched after commit) and integration events
/// (distributed, dispatched before commit for Outbox atomicity).
/// </summary>
/// <remarks>
/// <para>
/// Aggregate roots encapsulate invariants: external code should use methods
/// (e.g., <c>Approve()</c>, <c>Cancel()</c>) rather than setting properties directly.
/// </para>
/// <para>
/// Domain events are collected via <see cref="AddDomainEvent"/> and dispatched by the
/// <c>DomainEventDispatcherInterceptor</c> after <c>SaveChanges</c> commits.
/// </para>
/// <para>
/// Integration events are collected via <see cref="AddDistributedEvent"/> and dispatched
/// in <c>SavingChanges</c> (before commit) so that Wolverine persists outbox envelopes
/// atomically within the same EF Core transaction.
/// </para>
/// </remarks>
public abstract class AggregateRoot : Entity, IDomainEventSource, IIntegrationEventSource
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<IIntegrationEvent> _integrationEvents = [];

    /// <inheritdoc />
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyCollection<IIntegrationEvent> IntegrationEvents => _integrationEvents.AsReadOnly();

    /// <summary>
    /// Adds a domain event to be dispatched after the current transaction commits.
    /// </summary>
    /// <param name="domainEvent">The domain event to raise.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Adds an integration event to be dispatched before the current transaction commits,
    /// enabling Wolverine to persist the outbox envelope atomically.
    /// </summary>
    /// <param name="integrationEvent">The integration event to raise.</param>
    protected void AddDistributedEvent(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _integrationEvents.Add(integrationEvent);
    }

    /// <inheritdoc />
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <inheritdoc />
    public void ClearIntegrationEvents() => _integrationEvents.Clear();
}
