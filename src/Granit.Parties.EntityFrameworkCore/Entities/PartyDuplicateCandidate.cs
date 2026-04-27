using Granit.Domain;

namespace Granit.Parties.EntityFrameworkCore.Entities;

/// <summary>
/// Persisted candidate-duplicate pair surfaced by the recurring scan job
/// (<see href="https://github.com/granit-fx/granit-dotnet/issues/1300">#1300</see>) and
/// surfaced for review through the admin endpoints (#1301). Always stored as an
/// <em>ordered pair</em>: <see cref="PartyId"/> &lt; <see cref="CandidateId"/> by Guid
/// comparison, so the same pair is never represented twice with swapped ends.
/// </summary>
public sealed class PartyDuplicateCandidate : Entity, IMultiTenant
{
    private PartyDuplicateCandidate() { }

    /// <summary>
    /// Creates a new candidate. Caller MUST pass <paramref name="partyId"/> and
    /// <paramref name="candidateId"/> already ordered (party &lt; candidate); the EF
    /// configuration's UNIQUE constraint on <c>(TenantId, PartyId, CandidateId, Tier)</c>
    /// relies on this ordering.
    /// </summary>
    public static PartyDuplicateCandidate Create(
        Guid id,
        Guid? tenantId,
        Guid partyId,
        Guid candidateId,
        int tier,
        decimal score,
        string signalsJson,
        DateTimeOffset createdAt)
    {
        if (partyId.CompareTo(candidateId) >= 0)
        {
            throw new InvalidOperationException(
                "PartyDuplicateCandidate must be ordered: partyId < candidateId.");
        }
        return new PartyDuplicateCandidate
        {
            Id = id,
            TenantId = tenantId,
            PartyId = partyId,
            CandidateId = candidateId,
            Tier = tier,
            Score = score,
            SignalsJson = signalsJson,
            CreatedAt = createdAt,
        };
    }

    /// <summary>Owning tenant; <c>null</c> for host-scoped parties.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>The lower id of the ordered pair (Guid comparison).</summary>
    public Guid PartyId { get; private set; }

    /// <summary>The higher id of the ordered pair.</summary>
    public Guid CandidateId { get; private set; }

    /// <summary>Detection tier — <see cref="Granit.Parties.Deduplication.Domain.DuplicateMatchTier"/>
    /// stored as int so the EntityFrameworkCore module does not depend on the Deduplication
    /// package (the package depends the other way). The Deduplication module casts at the boundary.</summary>
    public int Tier { get; private set; }

    /// <summary>Aggregated confidence in <c>[0.0, 1.0]</c>.</summary>
    public decimal Score { get; private set; }

    /// <summary>JSON-serialised <c>MatchSignal[]</c> with the per-signal contributions.</summary>
    public string SignalsJson { get; private set; } = string.Empty;

    /// <summary>When an admin marked the pair as "not a duplicate" via the dismiss endpoint
    /// (<see href="https://github.com/granit-fx/granit-dotnet/issues/1301">#1301</see>).
    /// Dismissed candidates are skipped on subsequent scans by the upsert sink so the
    /// admin's decision is durable.</summary>
    public DateTimeOffset? DismissedAt { get; private set; }

    /// <summary>Initial scan timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Last refresh — bumped on every UPSERT so admins can sort by "freshest evidence first".</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Updates the score / tier / signals from a fresh scan, bumping <see cref="UpdatedAt"/>.</summary>
    public void Refresh(int tier, decimal score, string signalsJson, DateTimeOffset updatedAt)
    {
        Tier = tier;
        Score = score;
        SignalsJson = signalsJson;
        UpdatedAt = updatedAt;
    }

    /// <summary>Marks the candidate as dismissed by an admin; idempotent.</summary>
    public void Dismiss(DateTimeOffset dismissedAt) => DismissedAt = dismissedAt;
}
