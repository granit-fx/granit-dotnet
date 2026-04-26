using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when an external provider identifier is registered against a contact.</summary>
public sealed record ContactExternalMappingAddedEvent(
    ContactId ContactId,
    Guid? TenantId,
    string ProviderName,
    string ExternalId) : IDomainEvent;
