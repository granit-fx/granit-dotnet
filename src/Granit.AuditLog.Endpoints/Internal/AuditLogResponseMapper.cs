using Granit.AuditLog.Domain;
using Granit.AuditLog.Endpoints.Dtos;

namespace Granit.AuditLog.Endpoints.Internal;

/// <summary>
/// Maps <see cref="AuditLogEntry"/> domain entities to response DTOs.
/// </summary>
internal static class AuditLogResponseMapper
{
    /// <summary>Maps to a summary response (list item).</summary>
    public static AuditLogEntryResponse ToSummaryResponse(AuditLogEntry entry) =>
        new(
            entry.Id,
            entry.Timestamp,
            entry.UserId,
            entry.UserName,
            entry.Category.ToString(),
            entry.IpAddress,
            entry.TenantId,
            entry.CorrelationId,
            entry.EntityChanges.Count);

    /// <summary>Maps to a detailed response with full change hierarchy.</summary>
    public static AuditLogEntryDetailResponse ToDetailResponse(AuditLogEntry entry) =>
        new(
            entry.Id,
            entry.Timestamp,
            entry.UserId,
            entry.UserName,
            entry.Category.ToString(),
            entry.IpAddress,
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
