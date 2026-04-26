using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Integration event for contact reactivation.</summary>
public sealed record ContactActivatedEto(ContactId ContactId, Guid? TenantId) : IIntegrationEvent;
