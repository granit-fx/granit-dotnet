using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Domain;
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
            entityType,
            entityId,
            entryType,
            body,
            context.CurrentUser.UserId ?? string.Empty,
            context.CurrentUser.UserName ?? string.Empty,
            context.Clock.Now,
            context.CurrentUser.UserId ?? string.Empty,
            context.CurrentTenant.IsAvailable ? context.CurrentTenant.Id : null,
            parentEntryId);

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
            fileName,
            contentType,
            sizeBytes,
            context.Clock.Now,
            context.CurrentUser.UserId ?? string.Empty,
            context.CurrentTenant.IsAvailable ? context.CurrentTenant.Id : null);
}
