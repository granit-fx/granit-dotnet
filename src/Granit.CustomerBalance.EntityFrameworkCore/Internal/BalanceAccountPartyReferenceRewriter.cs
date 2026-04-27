using Granit.CustomerBalance.Domain;
using Granit.Mergeable;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Granit.CustomerBalance.EntityFrameworkCore.Internal;

/// <summary>
/// Bulk SQL rewriter that redirects <see cref="BalanceAccount.PartyId"/> from a
/// merged-out (loser) party onto the surviving party. Discovered automatically by the
/// merge orchestrator (<c>EfMergeService&lt;Party&gt;</c>) via DI registration as
/// <c>IReferenceRewriter&lt;Party&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Only the <c>BalanceAccount.PartyId</c> column is rewritten. The downstream
/// <c>BalanceTransaction</c> rows reference the account via <c>AccountId</c> (the
/// account's primary key), not the party — re-pointing the account onto the survivor
/// preserves the entire ledger history without touching individual transactions.
/// </para>
/// <para>
/// <b>Cardinality caveat — same-currency collision.</b> The per-party, per-currency
/// uniqueness constraint on <c>BalanceAccount</c> means that if loser and survivor both
/// hold an account in the same currency, this rewriter's bulk <c>UPDATE</c> would
/// produce two rows violating the unique index — the database surfaces the error and
/// the orchestrator's <see cref="System.Transactions.TransactionScope"/> rolls back the
/// entire merge. v1 deliberately lets this fail rather than silently consolidate, so
/// the operator is forced to make the consolidation decision explicitly. Real-world
/// consolidation (sum balances, repoint transactions, soft-delete loser account, emit
/// audit event) is tracked as
/// <see href="https://github.com/granit-fx/granit-dotnet/issues/1402">tech-debt #1402
/// — IMergeConsolidator&lt;T&gt; + BalanceAccount consolidation</see>; until that lands,
/// admins must reconcile the loser's balance off-merge and re-attempt.
/// </para>
/// <para>
/// Lives next to <c>CustomerBalanceDbContext</c> rather than in a dedicated
/// <c>Granit.CustomerBalance.Mergeable</c> package — same rationale as
/// <c>InvoicePartyReferenceRewriter</c> (#1288) and
/// <c>SubscriptionPartyReferenceRewriter</c> (#1289): the rewriter is a SQL/persistence
/// concern next to the DbContext that owns the FK; a dedicated package would be
/// plumbing for plumbing's sake.
/// </para>
/// </remarks>
internal sealed class BalanceAccountPartyReferenceRewriter(
    IDbContextFactory<CustomerBalanceDbContext> contextFactory) : IReferenceRewriter<Party>
{
    /// <inheritdoc />
    public string Description => "BalanceAccount.PartyId";

    /// <inheritdoc />
    public async Task<int> RewriteAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken)
    {
        await using CustomerBalanceDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var loser = PartyId.Create(loserId);
        var survivor = PartyId.Create(survivorId);

        return await db.Accounts
            .Where(a => a.PartyId == loser)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PartyId, survivor), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken)
    {
        await using CustomerBalanceDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var loser = PartyId.Create(loserId);

        return await db.Accounts
            .Where(a => a.PartyId == loser)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
