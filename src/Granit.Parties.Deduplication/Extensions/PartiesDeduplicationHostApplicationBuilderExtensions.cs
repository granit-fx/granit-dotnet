using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Deduplication.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Parties.Deduplication.Extensions;

/// <summary>
/// DI registration helpers for the Granit.Parties duplicate-detection 3-tier pipeline.
/// </summary>
public static class PartiesDeduplicationHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the three internal matchers + the composer behind
    /// <see cref="IPartyDuplicateDetector"/>. Call after
    /// <c>AddGranitPartiesEntityFrameworkCore(...)</c> on the host builder so the
    /// canonical projections + <c>PartyDeduplicationOptions</c> are already in scope.
    /// </summary>
    public static IHostApplicationBuilder AddGranitPartiesDeduplication(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddScoped<Tier1DeterministicMatcher>();
        builder.Services.AddScoped<Tier2TrigramBlocker>();
        builder.Services.AddScoped<Tier3WeightedScorer>();
        builder.Services.TryAddScoped<IPartyDuplicateDetector, DefaultPartyDuplicateDetector>();
        builder.Services.TryAddScoped<IPartyDuplicateCandidateStore, EfPartyDuplicateCandidateStore>();

        return builder;
    }
}
