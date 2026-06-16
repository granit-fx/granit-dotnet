using Granit.Auditing.Domain;
using Granit.Auditing.Dtos;
using Granit.Auditing.Endpoints.Dtos;

namespace Granit.Auditing.Endpoints.Internal;

/// <summary>
/// Maps <see cref="AuditEntry"/> domain entities to response DTOs.
/// </summary>
internal static class AuditingResponseMapper
{
    /// <summary>Maps to the summary projection used by per-entity list lookups.</summary>
    public static AuditEntryResponse ToSummaryResponse(AuditEntry entry) =>
        new(
            entry.Id,
            entry.Timestamp,
            entry.UserId,
            entry.UserName,
            entry.Category,
            entry.IpAddress,
            entry.TenantId,
            entry.CorrelationId,
            entry.EntityChanges.Count);

    /// <summary>Maps to a detailed response with full change hierarchy.</summary>
    public static AuditEntryDetailResponse ToDetailResponse(AuditEntry entry) =>
        new(
            entry.Id,
            entry.Timestamp,
            entry.UserId,
            entry.UserName,
            entry.Category.ToString(),
            entry.IpAddress,
            entry.UserAgent,
            entry.TenantId,
            entry.CorrelationId,
            entry.EntityChanges.Select(ToEntityChangeResponse).ToList());

    private static AuditEntityChangeResponse ToEntityChangeResponse(AuditEntityChange change) =>
        new(
            change.EntityType,
            change.EntityId,
            change.ChangeType.ToString(),
            change.PropertyChanges.Select(ToPropertyChangeResponse).ToList());

    private static AuditPropertyChangeResponse ToPropertyChangeResponse(AuditPropertyChange change) =>
        new(
            change.PropertyName,
            change.OriginalValue,
            change.NewValue);
}
