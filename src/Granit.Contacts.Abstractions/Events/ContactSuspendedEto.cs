using Granit.Contacts.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Contacts.Events;

/// <summary>Integration event for contact suspension.</summary>
public sealed record ContactSuspendedEto(
    ContactId ContactId,
    Guid? TenantId,
    string? Reason) : IIntegrationEvent;
