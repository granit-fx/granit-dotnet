using Granit.Domain;

namespace Granit.EntityMerge;

/// <summary>
/// Plug-in contract: "I am a module that holds references to <typeparamref name="TAggregate"/>,
/// and I know how to bulk-rewrite those references when one aggregate is merged into another."
/// </summary>
/// <remarks>
/// <para>
/// One implementation per module that owns a foreign key to <typeparamref name="TAggregate"/>.
/// Examples: <c>InvoicePartyReferenceRewriter</c> rewrites <c>Invoice.PartyId</c>;
/// <c>SubscriptionPartyReferenceRewriter</c> rewrites <c>Subscription.PartyId</c>; etc.
/// </para>
/// <para>
/// The merge orchestrator (<c>EfMergeService&lt;TAggregate&gt;</c>) discovers all registered
/// rewriters via DI and runs them in the same transaction as the survivor mutation. Single-
/// PostgreSQL deployments enrol all participating <c>DbContext</c>s in one
/// <c>TransactionScope</c> with <c>IsolationLevel.Serializable</c> — failure of any rewriter
/// rolls back everything atomically.
/// </para>
/// <para>
/// Implementations should use bulk SQL (<c>ExecuteUpdateAsync</c>) — never load entities into
/// the change tracker — for two reasons: (1) performance on tables with millions of rows,
/// (2) avoiding the EF child-collection merge pitfall (PK conflicts on tracked entities).
/// </para>
/// </remarks>
/// <typeparam name="TAggregate">The aggregate being merged (e.g. <c>Party</c>).</typeparam>
public interface IReferenceRewriter<TAggregate>
    where TAggregate : Entity
{
    /// <summary>
    /// Stable human-readable description of what this rewriter rewrites — surfaced in the
    /// merge preview UI and the audit log. E.g. <c>"Invoice.PartyId"</c>,
    /// <c>"Subscription.PartyId"</c>. Used as the key in <c>MergeResult.RewriteCounts</c>.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Rewrites every reference from <paramref name="loserId"/> to <paramref name="survivorId"/>
    /// and returns the number of rows actually updated.
    /// </summary>
    /// <param name="survivorId">Target id (the surviving aggregate).</param>
    /// <param name="loserId">Source id (the aggregate being merged out).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    Task<int> RewriteAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken);

    /// <summary>
    /// Dry-run counterpart of <see cref="RewriteAsync"/>: returns the number of rows that
    /// <em>would</em> be rewritten, without modifying anything. Used to populate the preview
    /// shown to the admin before they confirm a merge.
    /// </summary>
    Task<int> CountAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken);
}
