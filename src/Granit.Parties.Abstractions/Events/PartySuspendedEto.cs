using Granit.DataProtection;
using Granit.Encryption;
using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>Integration event for party suspension.</summary>
public sealed record PartySuspendedEto(
    PartyId PartyId,
    Guid? TenantId,
    [property: SensitiveData(Level = Sensitivity.Confidential), Encrypted] string? Reason)
    : IIntegrationEvent;
