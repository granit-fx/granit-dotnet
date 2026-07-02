using System.Diagnostics.CodeAnalysis;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Privacy.DataExport;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Notifications.Privacy.DataDeletion;

/// <summary>
/// Wolverine handler that erases a data subject's notification data — in-app inbox,
/// preferences and topic/entity subscriptions — on a
/// <see cref="PersonalDataDeletionRequestedEto"/> (GDPR Art. 17). Hard deletes via
/// <see cref="INotificationsPersonalDataEraser.EraseUserDataAsync"/> — soft delete would leave
/// notification content recoverable, which the right to erasure forbids.
/// </summary>
/// <remarks>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c>; the class is therefore public
/// with a public constructor and a public static Handle method. The eraser is idempotent, so a
/// Wolverine retry after a transient failure converges.
/// <para>
/// On success the handler returns a <see cref="PersonalDataDeletedEto"/> acknowledgement carrying
/// the exact provider name this module registers
/// (<see cref="NotificationsPrivacyDataProvider.ProviderName"/>). Wolverine cascades it back to the
/// deletion saga (routed by <c>[SagaIdentity]</c>), which drains this provider from its pending set.
/// On failure the eraser throws before the return, so no ack is published — Wolverine retries /
/// dead-letters and the saga surfaces the request as PartiallyExecuted.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class NotificationsPersonalDataDeletionHandler
{
    public static async Task<PersonalDataDeletedEto> Handle(
        PersonalDataDeletionRequestedEto @event,
        INotificationsPersonalDataEraser eraser,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(eraser);
        ArgumentNullException.ThrowIfNull(currentTenant);

        Guid? tenantId = @event.TenantId ?? currentTenant.Id;
        await eraser.EraseUserDataAsync(@event.UserId.ToString(), tenantId, cancellationToken).ConfigureAwait(false);

        return new PersonalDataDeletedEto(
            @event.RequestId,
            NotificationsPrivacyDataProvider.ProviderName,
            DeletionAction.PhysicalDelete,
            AffectedRecords: 0,
            Details: null,
            @event.TenantId);
    }
}
