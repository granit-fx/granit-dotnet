using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Domain event raised when a contact's PII is pseudonymised under a GDPR Article 17 erasure request.</summary>
public sealed record ContactPersonalDataPseudonymizedEvent(ContactId ContactId, Guid? TenantId) : IDomainEvent;
