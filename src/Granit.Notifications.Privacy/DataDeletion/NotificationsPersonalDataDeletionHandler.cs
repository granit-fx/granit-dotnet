using System.Diagnostics.CodeAnalysis;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
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
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class NotificationsPersonalDataDeletionHandler
{
    public static async Task Handle(
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
    }
}
