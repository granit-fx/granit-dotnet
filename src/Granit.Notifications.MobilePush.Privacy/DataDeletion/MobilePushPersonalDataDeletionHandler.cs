using System.Diagnostics.CodeAnalysis;
using Granit.MultiTenancy;
using Granit.Notifications.MobilePush.Privacy.DataExport;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Notifications.MobilePush.Privacy.DataDeletion;

/// <summary>
/// Wolverine handler that erases a data subject's mobile push device tokens on a
/// <see cref="PersonalDataDeletionRequestedEto"/> (GDPR Art. 17). Hard deletes via the
/// writer's bulk <c>EraseUserDataAsync</c> — the identifiers are credentials, and the
/// right to erasure forbids leaving them recoverable.
/// </summary>
/// <remarks>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c>; the class is therefore public
/// with a public constructor and a public static Handle method. The erasure is idempotent, so a
/// Wolverine retry after a transient failure converges. On success the acknowledgement carries
/// the exact provider name this module registers and the REAL deleted row count (ISO 27001
/// deletion evidence); on failure the writer throws before the return, so no ack is published —
/// the saga surfaces the request as PartiallyExecuted.
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class MobilePushPersonalDataDeletionHandler
{
    public static async Task<PersonalDataDeletedEto> Handle(
        PersonalDataDeletionRequestedEto @event,
        IMobilePushTokenWriter writer,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(currentTenant);

        Guid? tenantId = @event.TenantId ?? currentTenant.Id;
        int affectedRecords = await writer
            .EraseUserDataAsync(@event.UserId.ToString(), tenantId, cancellationToken).ConfigureAwait(false);

        return new PersonalDataDeletedEto(
            @event.RequestId,
            MobilePushPrivacyDataProvider.ProviderName,
            DeletionAction.PhysicalDelete,
            affectedRecords,
            Details: null,
            @event.TenantId);
    }
}
