using System.Diagnostics.CodeAnalysis;
using Granit.Auditing.Options;
using Granit.Auditing.Privacy.DataExport;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.Privacy.DataDeletion;

/// <summary>
/// Wolverine handler that processes a data subject's audit trail on a
/// <see cref="PersonalDataDeletionRequestedEto"/>. The audit <em>events</em> are always
/// retained — an immutable, append-only record of security-relevant events is an ISO 27001
/// A.12.4 control and, under GDPR Art. 17(3)(b), erasure does not apply where processing is
/// necessary for compliance with a legal obligation. By default the subject's <em>direct
/// identifiers</em> (user id, username, IP, user-agent) are pseudonymized in place, so both
/// obligations are met without manual intervention; set
/// <c>Auditing:PseudonymizeOnErasure = false</c> to retain the trail untouched.
/// </summary>
/// <remarks>
/// <para>
/// Auditing registers itself with the privacy data-provider registry (for the Art. 15 export
/// of a subject's own audit entries), so the deletion saga snapshots <c>"auditing"</c> into
/// its expected acknowledgement set — this handler must always acknowledge, whatever the
/// configured behavior, or every deletion would time out to <c>PartiallyExecuted</c>.
/// </para>
/// <para>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c>; the class is therefore
/// public with a public constructor and a public static Handle method. Pseudonymization is
/// idempotent under Wolverine at-least-once redelivery: a redelivered event matches zero
/// rows because the stored user id is already the hash.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class AuditingPersonalDataDeletionHandler
{
    public static async Task<PersonalDataDeletedEto> HandleAsync(
        PersonalDataDeletionRequestedEto @event,
        IAuditingCleaner cleaner,
        IOptions<AuditingOptions> options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Value.PseudonymizeOnErasure)
        {
            return new PersonalDataDeletedEto(
                @event.RequestId,
                AuditingPrivacyDataProvider.ProviderName,
                DeletionAction.Retained,
                AffectedRecords: 0,
                Details: "Audit trail retained for legal/compliance obligations (GDPR Art. 17(3)(b), ISO 27001 A.12.4).",
                @event.TenantId);
        }

        int affected = await cleaner
            .PseudonymizeByUserAsync(@event.UserId.ToString(), cancellationToken)
            .ConfigureAwait(false);

        return new PersonalDataDeletedEto(
            @event.RequestId,
            AuditingPrivacyDataProvider.ProviderName,
            DeletionAction.Anonymized,
            AffectedRecords: affected,
            Details: "Audit events retained (GDPR Art. 17(3)(b), ISO 27001 A.12.4); direct identifiers pseudonymized in place.",
            @event.TenantId);
    }
}
