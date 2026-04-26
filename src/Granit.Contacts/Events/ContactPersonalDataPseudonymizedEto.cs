using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Integration event signalling that a contact's PII has been pseudonymised (GDPR Article 17).</summary>
public sealed record ContactPersonalDataPseudonymizedEto(ContactId ContactId, Guid? TenantId) : IIntegrationEvent;
