using Granit.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Domain;
using Granit.Timeline.Domain.ValueObjects;
using Granit.Timing;
using Granit.Users;

namespace Granit.Timeline.Internal;

/// <summary>
/// Groups the infrastructure services required for audit-field initialization.
/// </summary>
internal sealed record AuditContext(
    IGuidGenerator GuidGenerator,
    IClock Clock,
    ICurrentUserService CurrentUser,
    ICurrentTenant CurrentTenant);

/// <summary>
/// Centralizes <see cref="TimelineEntry"/> and <see cref="TimelineAttachment"/> construction
/// to avoid duplicating audit-field initialization across store implementations.
/// </summary>
internal static class TimelineEntityFactory
{
    internal static TimelineEntry CreateEntry(
        string entityType,
        string entityId,
        TimelineEntryType entryType,
        string body,
        Guid? parentEntryId,
        AuditContext context) =>
        TimelineEntry.Create(
            context.GuidGenerator.Create(),
            new EntityReference(entityType, entityId),
            entryType,
            body,
            new AuthorInfo(
                context.CurrentUser.UserId ?? string.Empty,
                context.CurrentUser.UserName ?? string.Empty),
            context.Clock.Now,
            context.CurrentUser.UserId ?? string.Empty,
            context.CurrentTenant.IsAvailable ? context.CurrentTenant.Id : null,
            parentEntryId);

    internal static TimelineEntry CreateShadow(
        string entityType,
        string entityId,
        string sourceKey,
        string sourceId,
        TimelineStreamEntry projection,
        AuditContext context)
    {
        Guid? tenantId = context.CurrentTenant.IsAvailable ? context.CurrentTenant.Id : null;
        Guid id = TimelineAnchor.ComputeShadowId(tenantId, entityType, entityId, sourceKey, sourceId);

        return TimelineEntry.CreateShadow(
            id,
            new EntityReference(entityType, entityId),
            projection.Body,
            new AuthorInfo(projection.AuthorId ?? string.Empty, projection.AuthorName ?? string.Empty),
            projection.OccurredAt,
            context.CurrentUser.UserId ?? string.Empty,
            sourceKey,
            sourceId,
            tenantId);
    }

    internal static TimelineAttachment CreateAttachment(
        Guid entryId,
        Guid blobId,
        string fileName,
        string contentType,
        long sizeBytes,
        AuditContext context) =>
        TimelineAttachment.Create(
            context.GuidGenerator.Create(),
            entryId,
            blobId,
            new FileMetadata(fileName, contentType, sizeBytes),
            context.Clock.Now,
            context.CurrentUser.UserId ?? string.Empty,
            context.CurrentTenant.IsAvailable ? context.CurrentTenant.Id : null);
}
