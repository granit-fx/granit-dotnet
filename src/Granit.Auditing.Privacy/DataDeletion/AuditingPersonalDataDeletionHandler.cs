using System.Diagnostics.CodeAnalysis;
using Granit.Auditing.Privacy.DataExport;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Auditing.Privacy.DataDeletion;

/// <summary>
/// Wolverine handler that acknowledges — without erasing — a data subject's audit trail on a
/// <see cref="PersonalDataDeletionRequestedEto"/>. The audit trail is intentionally retained: an
/// immutable, append-only record of security-relevant events is an ISO 27001 A.12.4 control and,
/// under GDPR Art. 17(3)(b), erasure does not apply where processing is necessary for compliance
/// with a legal obligation. The trail therefore survives the erasure by design.
/// </summary>
/// <remarks>
/// <para>
/// Auditing registers itself with the privacy data-provider registry (for the Art. 15 export of a
/// subject's own audit entries), so the deletion saga snapshots <c>"auditing"</c> into its expected
/// acknowledgement set. Without an acknowledgement the saga would wait for this provider forever and
/// every deletion would time out to <c>PartiallyExecuted</c>. This handler closes that gap by
/// emitting a <see cref="PersonalDataDeletedEto"/> with <see cref="DeletionAction.Retained"/> and
/// zero affected records — a truthful "nothing erased, retention is deliberate" acknowledgement that
/// drains the provider from the saga's pending set.
/// </para>
/// <para>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c>; the class is therefore public
/// with a public constructor and a public static Handle method. The acknowledgement is a pure,
/// side-effect-free projection of the incoming event, so it is trivially idempotent under
/// Wolverine at-least-once redelivery — the saga's fan-in dedupes duplicates.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class AuditingPersonalDataDeletionHandler
{
    public static PersonalDataDeletedEto Handle(PersonalDataDeletionRequestedEto @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return new PersonalDataDeletedEto(
            @event.RequestId,
            AuditingPrivacyDataProvider.ProviderName,
            DeletionAction.Retained,
            AffectedRecords: 0,
            Details: "Audit trail retained for legal/compliance obligations (GDPR Art. 17(3)(b), ISO 27001 A.12.4).",
            @event.TenantId);
    }
}
