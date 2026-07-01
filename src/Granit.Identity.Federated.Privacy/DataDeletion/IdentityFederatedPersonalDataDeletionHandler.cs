using System.Diagnostics.CodeAnalysis;
using Granit.MultiTenancy;
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
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class IdentityFederatedPersonalDataDeletionHandler
{
    public static async Task Handle(
        PersonalDataDeletionRequestedEto @event,
        IFederatedUserCacheEraser cacheEraser,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(cacheEraser);
        ArgumentNullException.ThrowIfNull(currentTenant);

        Guid? tenantId = @event.TenantId ?? currentTenant.Id;
        await cacheEraser.EraseAsync(@event.UserId.ToString(), tenantId, cancellationToken).ConfigureAwait(false);
    }
}
