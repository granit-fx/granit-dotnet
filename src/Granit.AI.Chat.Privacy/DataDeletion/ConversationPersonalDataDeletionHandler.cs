using System.Diagnostics.CodeAnalysis;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.AI.Chat.Privacy.DataDeletion;

/// <summary>
/// Wolverine handler that erases a data subject's conversations (and their messages) on a
/// <see cref="PersonalDataDeletionRequestedEto"/> (GDPR Art. 17). Hard deletes via
/// <see cref="IConversationDataManager.EraseOwnerAsync"/> — soft delete would leave the message
/// content recoverable, which the right to erasure forbids.
/// </summary>
/// <remarks>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c>; the class is therefore public
/// with a public constructor and a public static Handle method. The eraser is idempotent, so a
/// Wolverine retry after a transient failure converges. Transient attachment blobs are owned by the
/// application's blob store, which erases them through its own deletion handler.
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class ConversationPersonalDataDeletionHandler
{
    public static async Task Handle(
        PersonalDataDeletionRequestedEto @event,
        IConversationDataManager dataManager,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(dataManager);
        ArgumentNullException.ThrowIfNull(currentTenant);

        Guid? tenantId = @event.TenantId ?? currentTenant.Id;
        await dataManager.EraseOwnerAsync(tenantId, @event.UserId, cancellationToken).ConfigureAwait(false);
    }
}
