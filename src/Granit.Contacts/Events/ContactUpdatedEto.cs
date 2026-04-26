using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Integration event published when a contact's billing identity is updated.</summary>
public sealed record ContactUpdatedEto(ContactId ContactId, Guid? TenantId) : IIntegrationEvent;
