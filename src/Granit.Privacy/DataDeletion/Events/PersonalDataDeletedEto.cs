using Granit.Core.Events;

namespace Granit.Privacy.DataDeletion.Events;

/// <summary>
/// Published by each data provider after handling a personal data deletion request.
/// Provides a complete audit trail of what was done (ISO 27001 compliance).
/// </summary>
public sealed record PersonalDataDeletedEto(
    Guid RequestId,
    string ProviderName,
    DeletionAction Action,
    int AffectedRecords,
    string? Details) : IIntegrationEvent;
