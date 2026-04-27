using Granit.Mergeable;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

/// <summary>
/// Bulk SQL rewriter that redirects <see cref="Subscription.PartyId"/> from a merged-out
/// (loser) party onto the surviving party. Discovered automatically by the merge
/// orchestrator (<c>EfMergeService&lt;Party&gt;</c>) via DI registration as
/// <c>IReferenceRewriter&lt;Party&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Active subscriptions are rewritten too. The legal billing relationship transfers to
/// the survivor: the next renewal cycle bills the survivor's payment instrument, the
/// surviving billing address, and so on. Tax computation runs against the survivor's
/// <c>TaxStatus</c> (which has already been resolved by <c>Party.MergeFrom</c>), so a
/// reverse-charge or VAT-exempt loser correctly surfaces on the survivor's renewals
/// after merge.
/// </para>
/// <para>
/// Lives next to <c>SubscriptionsDbContext</c> rather than in a dedicated
/// <c>Granit.Subscriptions.Mergeable</c> package — the rewriter is intrinsically a
/// SQL/persistence concern and a separate package would be plumbing for plumbing's
/// sake. <c>Granit.Subscriptions.EntityFrameworkCore</c> takes a contracts-only
/// dependency on <c>Granit.Mergeable</c> (no runtime). Hosts that don't enable merging
/// pay only the DI registration line; with no <c>IMergeService&lt;Party&gt;</c> wired
/// up the rewriter is a no-op.
/// </para>
/// </remarks>
internal sealed class SubscriptionPartyReferenceRewriter(
    IDbContextFactory<SubscriptionsDbContext> contextFactory) : IReferenceRewriter<Party>
{
    /// <inheritdoc />
    public string Description => "Subscription.PartyId";

    /// <inheritdoc />
    public async Task<int> RewriteAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken)
    {
        await using SubscriptionsDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var loser = PartyId.Create(loserId);
        var survivor = PartyId.Create(survivorId);

        return await db.Subscriptions
            .Where(s => s.PartyId == loser)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PartyId, survivor), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken)
    {
        await using SubscriptionsDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var loser = PartyId.Create(loserId);

        return await db.Subscriptions
            .Where(s => s.PartyId == loser)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
