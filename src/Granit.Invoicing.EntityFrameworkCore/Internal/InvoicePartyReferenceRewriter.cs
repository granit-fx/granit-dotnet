using Granit.Invoicing.Domain;
using Granit.Mergeable;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Granit.Invoicing.EntityFrameworkCore.Internal;

/// <summary>
/// Bulk SQL rewriter that redirects <see cref="Invoice.PartyId"/> from a merged-out
/// (loser) party onto the surviving party. Discovered automatically by the merge
/// orchestrator (<c>EfMergeService&lt;Party&gt;</c>) via DI registration as
/// <c>IReferenceRewriter&lt;Party&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Finalised / paid invoices are rewritten too.</b> They snapshot the customer's
/// billing address at finalisation time
/// (<see cref="Invoice.IssuedBillingAddressSnapshot"/> is captured on issue), so
/// re-pointing <c>PartyId</c> does not change what was billed or shipped — the legal
/// document stays intact. The point of the rewrite is that all reports / aggregations
/// that group invoices by party (revenue per customer, ledger reconciliation, customer
/// statements) should converge on the surviving party. Leaving a finalised invoice
/// pointing at a tombstoned loser would force every read path to call
/// <c>PartyId.ResolveCurrentAsync</c>, which is fine for late-arriving messages but
/// wasteful for primary persistence.
/// </para>
/// <para>
/// <b>No optimistic-concurrency check on the invoice rows.</b> The orchestrator already
/// holds the per-tenant advisory lock that serialises concurrent merges, plus the
/// <c>FOR UPDATE</c> row lock on both Party rows. Bulk <c>ExecuteUpdateAsync</c> bypasses
/// the change tracker entirely so there is no <c>RowVersion</c> contention to worry
/// about.
/// </para>
/// <para>
/// Lives next to <c>InvoicingDbContext</c> rather than in a dedicated
/// <c>Granit.Invoicing.Mergeable</c> package: the rewriter is intrinsically a SQL/
/// persistence concern, so a separate package would be plumbing for plumbing's sake.
/// Granit.Invoicing.EntityFrameworkCore takes a lightweight contracts dependency on
/// Granit.Mergeable (no runtime), and the host opts into the merge orchestrator by
/// registering <c>IMergeService&lt;Party&gt;</c> separately.
/// </para>
/// </remarks>
internal sealed class InvoicePartyReferenceRewriter(
    IDbContextFactory<InvoicingDbContext> contextFactory) : IReferenceRewriter<Party>
{
    /// <inheritdoc />
    public string Description => "Invoice.PartyId";

    /// <inheritdoc />
    public async Task<int> RewriteAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken)
    {
        await using InvoicingDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var loser = PartyId.Create(loserId);
        var survivor = PartyId.Create(survivorId);

        return await db.Invoices
            .Where(i => i.PartyId == loser)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.PartyId, survivor), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken)
    {
        await using InvoicingDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var loser = PartyId.Create(loserId);

        return await db.Invoices
            .Where(i => i.PartyId == loser)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
