namespace Granit.Mergeable.EntityFrameworkCore.Domain;

/// <summary>
/// Cached merge result keyed by an idempotency key. A second call with the same key + same
/// request hash replays the cached result; a different hash with the same key is rejected
/// with a 409. Garbage-collected after 24h by a recurring background job (out of scope of
/// this package — host configures retention).
/// </summary>
internal sealed class MergeIdempotencyEntry
{
    public required Guid Id { get; init; }

    /// <summary>
    /// Tenant id for the merge that produced the entry (<c>null</c> in single-tenant deployments
    /// or for global merges). Always part of the lookup key — the orchestrator never reads or
    /// returns an entry from a different tenant, even if the idempotency key collides
    /// across tenants.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>Idempotency key supplied by the caller (HTTP <c>Idempotency-Key</c> header).</summary>
    public required string Key { get; init; }

    /// <summary>
    /// HMAC-SHA-256 of the canonicalised request body (survivorId + loserId + choices) keyed
    /// with the deployment-bound MAC key derived from <c>IStringEncryptionService</c>.
    /// </summary>
    public required string RequestHash { get; init; }

    /// <summary>Survivor id (the merge target).</summary>
    public required Guid SurvivorId { get; init; }

    /// <summary>Loser id (the merge source).</summary>
    public required Guid LoserId { get; init; }

    /// <summary>
    /// Encrypted JSON payload of the cached <c>MergeResult</c> projection (rewrite counts +
    /// field-conflict <em>paths</em> + winner sides only — NEVER the survivor/loser scalar
    /// values). Stored ciphertext via <c>IStringEncryptionService.Encrypt</c> so a database
    /// read does not surface tenant data; integrity is verified on replay against
    /// <see cref="ResultMac"/>.
    /// </summary>
    public required string ResultJson { get; init; }

    /// <summary>
    /// HMAC-SHA-256 of the plaintext <c>ResultJson</c> payload, keyed with the deployment-bound
    /// MAC key. Verified before deserialisation on replay — mismatching MAC rejects the row
    /// (CWE-353 / encrypt-then-MAC pattern).
    /// </summary>
    public required string ResultMac { get; init; }

    /// <summary>UTC timestamp when the entry was written.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
