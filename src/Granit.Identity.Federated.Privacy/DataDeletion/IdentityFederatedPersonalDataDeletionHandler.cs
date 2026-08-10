using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Federated.Privacy.DataExport;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Identity.Federated.Privacy.DataDeletion;

/// <summary>
/// Wolverine handler that erases a data subject's local federated identity cache entry
/// (profile mirror + sync metadata) on a <see cref="PersonalDataDeletionRequestedEto"/>
/// (GDPR Art. 17). Hard deletes via <see cref="IFederatedUserCacheEraser.EraseAsync"/> — this
/// is a local cache only, the identity provider (Keycloak, Entra ID, Cognito, ...) remains the
/// source of truth and is not touched by this handler.
/// </summary>
/// <remarks>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c>; the class is therefore public
/// with a public constructor and a public static Handle method. The eraser is idempotent, so a
/// Wolverine retry after a transient failure converges.
/// <para>
/// On success the handler returns a <see cref="PersonalDataDeletedEto"/> acknowledgement carrying
/// the exact provider name this module registers
/// (<see cref="IdentityFederatedPrivacyDataProvider.ProviderName"/>). Wolverine cascades it back to
/// the deletion saga (routed by <c>[SagaIdentity]</c>), which drains this provider from its pending
/// set. On failure the eraser throws before the return, so no ack is published — Wolverine retries
/// / dead-letters and the saga surfaces the request as PartiallyExecuted.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class IdentityFederatedPersonalDataDeletionHandler
{
    public static async Task<PersonalDataDeletedEto> Handle(
        PersonalDataDeletionRequestedEto @event,
        IFederatedUserCacheEraser cacheEraser,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(cacheEraser);
        ArgumentNullException.ThrowIfNull(currentTenant);

        // Distributed dispatch carries no ambient tenant — establish it from the event so the
        // multi-tenant query filter exposes the rows the eraser's explicit predicate targets.
        // Without it the erasure silently no-ops on tenant-scoped mirrors while the saga acks.
        // A null resolved scope (no tenant on the event, none ambient) erases across ALL
        // partitions: Art. 17 must not leave a mirror behind in any scope.
        Guid? tenantId = @event.TenantId ?? (currentTenant.IsAvailable ? currentTenant.Id : null);
        using IDisposable _ = currentTenant.Change(tenantId);

        int affectedRecords = await cacheEraser
            .EraseAsync(@event.UserId.ToString(), tenantId, cancellationToken).ConfigureAwait(false);

        return new PersonalDataDeletedEto(
            @event.RequestId,
            IdentityFederatedPrivacyDataProvider.ProviderName,
            DeletionAction.PhysicalDelete,
            affectedRecords,
            Details: null,
            @event.TenantId);
    }
}
