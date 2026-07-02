using System.Diagnostics.CodeAnalysis;
using Granit.AI.Prompts.Privacy.DataExport;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.AI.Prompts.Privacy.DataDeletion;

/// <summary>
/// Wolverine handler that erases a data subject's own prompt templates on a
/// <see cref="PersonalDataDeletionRequestedEto"/> (GDPR Art. 17). Hard deletes via
/// <see cref="IPromptTemplateDataManager.EraseOwnerAsync"/> — soft delete would leave the prompt
/// content recoverable, which the right to erasure forbids. Framework-seeded system prompts are
/// never touched.
/// </summary>
/// <remarks>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c>; the class is therefore public
/// with a public constructor and a public static Handle method. The eraser is idempotent, so a
/// Wolverine retry after a transient failure converges.
/// <para>
/// On success the handler returns a <see cref="PersonalDataDeletedEto"/> acknowledgement carrying
/// the exact provider name this module registers
/// (<see cref="PromptTemplatePrivacyDataProvider.ProviderName"/>) and the number of rows erased.
/// Wolverine cascades it back to the deletion saga (routed by <c>[SagaIdentity]</c>), which drains
/// this provider from its pending set. On failure the eraser throws before the return, so no ack is
/// published — Wolverine retries / dead-letters and the saga surfaces the request as
/// PartiallyExecuted.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class PromptTemplatePersonalDataDeletionHandler
{
    public static async Task<PersonalDataDeletedEto> Handle(
        PersonalDataDeletionRequestedEto @event,
        IPromptTemplateDataManager dataManager,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(dataManager);
        ArgumentNullException.ThrowIfNull(currentTenant);

        Guid? tenantId = @event.TenantId ?? currentTenant.Id;
        int erased = await dataManager.EraseOwnerAsync(tenantId, @event.UserId, cancellationToken).ConfigureAwait(false);

        return new PersonalDataDeletedEto(
            @event.RequestId,
            PromptTemplatePrivacyDataProvider.ProviderName,
            DeletionAction.PhysicalDelete,
            erased,
            Details: null,
            @event.TenantId);
    }
}
