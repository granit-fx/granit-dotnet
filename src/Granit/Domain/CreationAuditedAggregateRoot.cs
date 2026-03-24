using Granit.Events;

namespace Granit.Domain;

/// <summary>
/// Aggregate root with creation-only audit trail (CreatedAt, CreatedBy).
/// Carries domain events (local, dispatched after commit) and integration events
/// (distributed, dispatched before commit for Outbox atomicity).
/// </summary>
public abstract class CreationAuditedAggregateRoot : CreationAuditedEntity, IDomainEventSource, IIntegrationEventSource
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
