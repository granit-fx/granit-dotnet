namespace Granit.DataExchange.Import.Identity;

/// <summary>
/// Result of resolving the identity of an imported record — a key-based, batch-friendly
/// description of which persistence operation to perform, without carrying a live entity
/// reference (resolvers run against their own short-lived contexts; only the executor's
/// context may track entities for save).
/// </summary>
/// <remarks>
/// Use the factory methods (<see cref="Insert"/>, <see cref="Update"/>, <see cref="Upsert"/>,
/// <see cref="Skip"/>, <see cref="Ambiguous"/>) rather than the object initializer directly —
/// they keep the combination of <see cref="Operation"/>, <see cref="Key"/>, and
/// <see cref="ExternalId"/> self-consistent.
/// </remarks>
public sealed record RecordIdentity
{
    /// <summary>The persistence operation to perform.</summary>
    public required RecordOperation Operation { get; init; }

    /// <summary>
    /// The key identifying the row for prefetch, or <c>null</c> for
    /// <see cref="RecordOperation.Insert"/>, <see cref="RecordOperation.Skip"/>,
    /// and <see cref="RecordOperation.Ambiguous"/>.
    /// </summary>
    public EntityKey? Key { get; init; }

    /// <summary>The kind of key held in <see cref="Key"/> — meaningless when <see cref="Key"/> is <c>null</c>.</summary>
    public EntityKeyKind KeyKind { get; init; }

    /// <summary>
    /// The external identifier from the source system, set when an external-ID definition
    /// resolved this row. Persisted by the executor once the row has actually been inserted.
    /// </summary>
    public string? ExternalId { get; init; }

    /// <summary>
    /// Structured reason codes (see <see cref="IdentityReasonCodes"/>) explaining a
    /// <see cref="RecordOperation.Skip"/> or <see cref="RecordOperation.Ambiguous"/> outcome.
    /// </summary>
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];

#pragma warning disable CA1000 // Intentional: typed factory methods keep Operation/Key/ExternalId self-consistent
    /// <summary>Creates an <see cref="RecordOperation.Insert"/> identity, optionally carrying an external ID to persist post-insert.</summary>
    /// <param name="externalId">The external identifier to map to the newly inserted row, if any.</param>
    public static RecordIdentity Insert(string? externalId = null) =>
        new() { Operation = RecordOperation.Insert, ExternalId = externalId };

    /// <summary>Creates a strict <see cref="RecordOperation.Update"/> identity — fails the row if no match is found.</summary>
    /// <param name="key">The key identifying the existing row.</param>
    /// <param name="keyKind">The kind of key.</param>
    /// <param name="externalId">The external identifier already mapped to this row, if any.</param>
    public static RecordIdentity Update(EntityKey key, EntityKeyKind keyKind, string? externalId = null) =>
        new() { Operation = RecordOperation.Update, Key = key, KeyKind = keyKind, ExternalId = externalId };

    /// <summary>Creates a <see cref="RecordOperation.Upsert"/> identity — inserts when no match is found.</summary>
    /// <param name="key">The key identifying the row.</param>
    /// <param name="keyKind">The kind of key.</param>
    public static RecordIdentity Upsert(EntityKey key, EntityKeyKind keyKind) =>
        new() { Operation = RecordOperation.Upsert, Key = key, KeyKind = keyKind };

    /// <summary>Creates a <see cref="RecordOperation.Skip"/> identity — the row is not persisted.</summary>
    /// <param name="reasonCodes">Structured reason codes (see <see cref="IdentityReasonCodes"/>).</param>
    public static RecordIdentity Skip(params ReadOnlySpan<string> reasonCodes) =>
        new() { Operation = RecordOperation.Skip, ReasonCodes = [.. reasonCodes] };

    /// <summary>Creates an <see cref="RecordOperation.Ambiguous"/> identity — the row fails as an identity error.</summary>
    /// <param name="reasonCodes">Structured reason codes (see <see cref="IdentityReasonCodes"/>).</param>
    public static RecordIdentity Ambiguous(params ReadOnlySpan<string> reasonCodes) =>
        new() { Operation = RecordOperation.Ambiguous, ReasonCodes = [.. reasonCodes] };
#pragma warning restore CA1000
}
