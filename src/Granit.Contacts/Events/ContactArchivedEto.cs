using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Integration event for contact archival.</summary>
public sealed record ContactArchivedEto(ContactId ContactId, Guid? TenantId) : IIntegrationEvent;
