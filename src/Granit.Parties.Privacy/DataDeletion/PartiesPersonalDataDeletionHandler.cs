using System.Diagnostics.CodeAnalysis;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Events;
using Granit.Parties.Domain;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Parties.Privacy.DataDeletion;

/// <summary>
/// Wolverine-discovered integration-event handler that fulfils the GDPR Article 17
/// erasure obligation for the <see cref="Party"/> aggregate. Pseudonymises every PII
/// field on the party linked to the requesting user while preserving the row for
/// accounting integrity, then publishes a <see cref="PersonalDataDeletedEto"/> audit
/// fragment for the privacy saga.
/// </summary>
/// <remarks>
/// <para>
/// The lookup goes through the <see cref="IDataFilter"/> with the <see cref="IMultiTenant"/>
/// filter disabled, because the deletion request is identified by user only — a single user
/// can be linked to a host-scoped or tenant-scoped party, and the privacy stack must
/// process either without leaking tenant context.
/// </para>
/// <para>
/// Pseudonymisation is idempotent: if the party has already been pseudonymised, the
/// handler emits a <see cref="DeletionAction.Retained"/> audit record with zero affected
/// records rather than re-publishing the domain event.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class PartiesPersonalDataDeletionHandler
{
    /// <summary>Provider name recorded on the audit fragment.</summary>
    public const string ProviderName = "parties";

    public static async Task HandleAsync(
        PersonalDataDeletionRequestedEto request,
        IPartyReader parties,
        IPartyWriter writer,
        IDataFilter dataFilter,
        IDistributedEventBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Party? party;
        using (dataFilter.Disable<IMultiTenant>())
        {
            party = await parties.GetByUserIdAsync(request.UserId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (party is null)
        {
            await bus.PublishAsync(new PersonalDataDeletedEto(
                request.RequestId,
                ProviderName,
                DeletionAction.Retained,
                AffectedRecords: 0,
                Details: "no party linked to user"), cancellationToken).ConfigureAwait(false);
            return;
        }

        bool changed = party.PseudonymizePersonalData();
        if (!changed)
        {
            await bus.PublishAsync(new PersonalDataDeletedEto(
                request.RequestId,
                ProviderName,
                DeletionAction.Retained,
                AffectedRecords: 0,
                Details: "party already pseudonymised"), cancellationToken).ConfigureAwait(false);
            return;
        }

        await writer.UpdateAsync(party, cancellationToken).ConfigureAwait(false);

        await bus.PublishAsync(new PersonalDataDeletedEto(
            request.RequestId,
            ProviderName,
            DeletionAction.Anonymized,
            AffectedRecords: 1,
            Details: $"party {party.Id} pseudonymised; row retained for accounting integrity"), cancellationToken).ConfigureAwait(false);
    }
}
