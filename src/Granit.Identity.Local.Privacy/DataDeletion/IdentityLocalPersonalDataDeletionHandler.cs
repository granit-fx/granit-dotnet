using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Local.Privacy.DataExport;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Identity.Local.Privacy.DataDeletion;

/// <summary>
/// Wolverine handler that erases a data subject's local identity account on a
/// <see cref="PersonalDataDeletionRequestedEto"/> (GDPR Art. 17). Delegates to
/// <see cref="IAccountDeletionService"/> — the same soft-delete, lockout, security-stamp
/// invalidation and token-revocation flow already exercised by the self-service account
/// deletion endpoint — so the framework has a single source of truth for what "erasing a
/// local identity account" means, whether triggered by the user or by the Privacy saga.
/// </summary>
/// <remarks>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c>; the class is therefore public
/// with a public constructor and a public static Handle method. <see cref="IAccountDeletionService.InitiateAsync"/>
/// takes no tenant parameter — it resolves the account through <c>UserManager</c>, which applies
/// whatever tenant is active in <see cref="ICurrentTenant"/> at call time. The handler therefore
/// opens an explicit tenant scope from the event before delegating, mirroring the
/// <c>@event.TenantId ?? currentTenant.Id</c> fallback used by the other reference deletion
/// handlers (e.g. <c>ConversationPersonalDataDeletionHandler</c>) rather than relying solely on
/// implicit restoration by Wolverine's <c>TenantContextBehavior</c> middleware.
/// <para>
/// On success the handler returns a <see cref="PersonalDataDeletedEto"/> acknowledgement, which
/// Wolverine cascades back to the deletion saga (routed by <c>[SagaIdentity]</c> on
/// <c>RequestId</c>). The saga drains this provider from its pending set; only when every provider
/// acknowledges does it mark the request Executed. The ack is emitted with the exact provider name
/// this module registers (<see cref="IdentityLocalPrivacyDataProvider.ProviderName"/>) so the
/// saga's set matches. If <see cref="IAccountDeletionService.InitiateAsync"/> throws, the return is
/// never reached, so no ack is published and Wolverine retries / dead-letters — the saga then
/// correctly surfaces the request as PartiallyExecuted rather than Executed.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class IdentityLocalPersonalDataDeletionHandler
{
    public static async Task<PersonalDataDeletedEto> Handle(
        PersonalDataDeletionRequestedEto @event,
        IAccountDeletionService deletionService,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(deletionService);
        ArgumentNullException.ThrowIfNull(currentTenant);

        Guid? tenantId = @event.TenantId ?? currentTenant.Id;
        using IDisposable scope = currentTenant.Change(tenantId);
        await deletionService.InitiateAsync(@event.UserId.ToString(), cancellationToken).ConfigureAwait(false);

        // Soft-delete + lockout + token revocation — the account row is retained in a
        // deactivated, unusable state (the identity subsystem's erasure contract), so the
        // audit action is SoftDelete.
        return new PersonalDataDeletedEto(
            @event.RequestId,
            IdentityLocalPrivacyDataProvider.ProviderName,
            DeletionAction.SoftDelete,
            AffectedRecords: 0,
            Details: null,
            @event.TenantId);
    }
}
