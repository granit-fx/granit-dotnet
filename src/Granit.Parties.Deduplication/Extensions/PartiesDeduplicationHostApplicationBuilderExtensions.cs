using Granit.DataExchange.Extensions;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Deduplication.EntityFrameworkCore;
using Granit.Parties.Deduplication.Exports;
using Granit.Parties.Deduplication.Internal;
using Granit.Parties.Deduplication.Queries;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Entities;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
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

        // QueryEngine wiring for the admin grid (#1301): query / export definitions plus
        // the IQueryableSource that powers MapGranitQuery<PartyDuplicateCandidate> in the
        // Endpoints package. Provider-agnostic — the source bypasses no filters and the
        // ambient IMultiTenant filter scopes every read to the current tenant.
        builder.Services.AddQueryDefinition<PartyDuplicateCandidate, DuplicateCandidateQueryDefinition>();
        builder.Services.AddExportDefinition<PartyDuplicateCandidate, DuplicateCandidateExportDefinition>();
        builder.Services.AddScoped<IQueryableSource<PartyDuplicateCandidate>, EfPartyDuplicateCandidateQueryableSource>();

        return builder;
    }
}
