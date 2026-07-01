using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
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
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class IdentityLocalPersonalDataDeletionHandler
{
    public static async Task Handle(
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
    }
}
