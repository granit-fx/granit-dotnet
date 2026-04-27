using Granit.Mergeable;
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
    /// Registers the per-aggregate adapter for parties. Cross-module reference rewriters
    /// (Invoice.PartyId, Subscription.PartyId, etc.) are NOT registered here — each module
    /// that holds a <c>PartyId</c> ships its own <c>*.Mergeable</c> package and registers
    /// its rewriter via <c>services.AddReferenceRewriter&lt;Party, ...&gt;()</c>.
    /// </summary>
    public static IHostApplicationBuilder AddGranitPartiesMergeable(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddScoped<IMergeableAggregateAdapter<Party>, PartyMergeableAggregateAdapter>();

        return builder;
    }
}
