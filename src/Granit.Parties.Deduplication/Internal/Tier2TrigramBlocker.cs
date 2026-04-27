using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.Parties.Deduplication.Internal;

/// <summary>
/// Tier-2 of the duplicate-detection pipeline: pg_trgm trigram-similarity blocking on the
/// party Name. PostgreSQL only — uses raw SQL with the <c>%</c> operator + <c>similarity()</c>
/// against the GIST index installed by
/// <c>PartiesPostgresMigrationExtensions.AddPartyTrigramSimilarityIndexes()</c>. On other
/// providers (SQL Server, SQLite, InMemory) the matcher returns an empty list — Tier-3 then
/// only re-ranks Tier-1 hits, gracefully degrading.
/// </summary>
/// <remarks>
/// <para>
/// Output is a candidate <em>set</em> meant to be re-ranked by Tier-3 — the score returned
/// here is raw trigram similarity in <c>[0.0, 1.0]</c>. Capped at <see cref="MaxCandidates"/>
/// to keep Tier-3 in-memory work bounded; a tenant with a million customers all named
/// "Acme" should not produce a million Tier-3 evaluations.
/// </para>
/// </remarks>
internal sealed class Tier2TrigramBlocker(
    IDbContextFactory<PartiesDbContext> contextFactory,
    IOptions<PartyDeduplicationOptions> options)
{
    /// <summary>Hard cap on the candidate set returned to Tier-3, regardless of threshold.</summary>
    public const int MaxCandidates = 50;

    private readonly PartyDeduplicationOptions _options = options.Value;

    public async Task<IReadOnlyList<DuplicateCandidate>> MatchAsync(
        PartyDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            return [];
        }

        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Non-PostgreSQL providers: Tier-2 is a no-op. Tier-3 still re-ranks Tier-1 hits;
        // the degradation is the loss of fuzzy-name discovery for parties without an
        // exact email/phone/VAT match.
        if (!string.Equals(db.Database.ProviderName, GranitDbProviders.Postgres, StringComparison.Ordinal))
        {
            return [];
        }

        double threshold = draft.Kind == PartyKind.Company
            ? _options.CompanySimilarityThreshold
            : _options.NameSimilarityThreshold;

        string schema = GranitPartiesDbProperties.DbSchema ?? "public";
        string table = GranitPartiesDbProperties.DbTablePrefix + "parties";

        // Raw SQL — EF translation of pg_trgm's `similarity()` and the `%` operator is not
        // built-in. The GIST index defined in story #1298 accelerates the `%` filter.
        // SQL parameters are positional: {0}=name, {1}=threshold, {2}=tenantId (or null).
        // The schema / table identifiers are interpolated at compose time (no SQL injection
        // risk — they come from framework-controlled GranitPartiesDbProperties).
        string sql =
            $"SELECT id, similarity(lower(name), lower({{0}})) AS sim "
            + $"FROM \"{schema}\".\"{table}\" "
            + "WHERE lower(name) % lower({0}) "
            + "  AND similarity(lower(name), lower({0})) >= {1} "
            + "  AND merged_into_id IS NULL "
            + "  AND (({2} IS NULL AND tenant_id IS NULL) OR (tenant_id = {2})) "
            + "ORDER BY sim DESC "
            + $"LIMIT {MaxCandidates}";

        List<TrigramRow> rows = await db.Database
            .SqlQueryRaw<TrigramRow>(sql, draft.Name, threshold, draft.TenantId!)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return [];
        }

        List<DuplicateCandidate> candidates = new(rows.Count);
        foreach (TrigramRow row in rows)
        {
            candidates.Add(new DuplicateCandidate(
                CandidateId: PartyId.Create(row.Id),
                Score: (decimal)row.Sim,
                Tier: DuplicateMatchTier.Blocking,
                Signals: [new MatchSignal("NameTrigram", (decimal)row.Sim)]));
        }

        return candidates;
    }

    /// <summary>Internal projection type for the raw-SQL query result.</summary>
#pragma warning disable CA1812 // Instantiated via reflection by EF Core (SqlQueryRaw projection).
    private sealed record TrigramRow(Guid Id, float Sim);
#pragma warning restore CA1812
}
