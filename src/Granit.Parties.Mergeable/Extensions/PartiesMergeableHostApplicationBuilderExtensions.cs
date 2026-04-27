using Granit.Mergeable;
using Granit.Mergeable.Extensions;
using Granit.Parties.Domain;
using Granit.Parties.Mergeable.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Parties.Mergeable.Extensions;

/// <summary>
/// Registration helper that wires the <see cref="Party"/> aggregate into the generic merge
/// orchestrator (<c>EfMergeService&lt;Party&gt;</c>). Apps already using
/// <c>AddGranitPartiesEntityFrameworkCore</c> + <c>AddGranitMergeableEntityFrameworkCore</c>
/// just call this method on top to make <see cref="IMergeService{Party}"/> resolvable.
/// </summary>
public static class PartiesMergeableHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the per-aggregate adapter for parties plus the two Parties-internal
    /// reference rewriters: <see cref="PartyParentReferenceRewriter"/> (re-parents Party
    /// children of the loser) and <see cref="PartyChildrenReferenceRewriter"/> (rewrites
    /// the four child collections — addresses, emails, phones, external mappings — via
    /// SQL bulk-update, with structural dedup, primary/default demotion and cap
    /// enforcement). Cross-module rewriters (Invoice.PartyId, Subscription.PartyId,
    /// BalanceAccount.PartyId, …) are NOT registered here — each consuming module
    /// inlines its rewriter inside its own <c>*.EntityFrameworkCore</c> package and
    /// registers it from its <c>AddGranit{Module}EntityFrameworkCore</c> extension via
    /// <c>services.AddReferenceRewriter&lt;Party, ...&gt;()</c>.
    /// </summary>
    public static IHostApplicationBuilder AddGranitPartiesMergeable(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddScoped<IMergeableAggregateAdapter<Party>, PartyMergeableAggregateAdapter>();
        builder.Services.AddReferenceRewriter<Party, PartyParentReferenceRewriter>();
        builder.Services.AddReferenceRewriter<Party, PartyChildrenReferenceRewriter>();

        return builder;
    }
}
