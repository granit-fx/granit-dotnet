using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Documents.Domain;

/// <summary>
/// Aggregate root tracking a tenant's <c>Granit.Documents</c> storage usage versus its
/// limit. Exactly one row per tenant — enforced by the unique index on
/// <c>(TenantId)</c> in the EF Core configuration.
/// </summary>
/// <remarks>
/// <para>
/// Created lazily on the first byte stored in a tenant (mirrors the tenant root folder
/// bootstrap from F2.2). <see cref="LimitBytes"/> seeds from
/// <c>GranitDocumentsOptions.DefaultTenantQuotaBytes</c>; hosts override per
/// subscription tier via the quota admin endpoint (F7.3).
/// </para>
/// <para>
/// <see cref="UsageBytes"/> is mutated through the service's atomic SQL UPDATE rather
/// than aggregate-level state changes — domain methods exist on this type for tests and
/// ad-hoc snapshot reads, but the production write path goes through
/// <c>ITenantQuotaService.Increment/DecrementAsync</c> which compose
/// <c>ExecuteUpdateAsync</c> with a single round-trip and no read-then-write race.
/// </para>
/// </remarks>
public sealed class TenantStorageQuota : AggregateRoot, IMultiTenant
{
    /// <summary>
    /// Creates a new quota row for the given tenant with the seed limit in bytes.
    /// </summary>
    public static TenantStorageQuota Create(Guid id, Guid? tenantId, long limitBytes, DateTimeOffset now)
    {
        if (limitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limitBytes), "Limit must be strictly positive.");
        }
        return new TenantStorageQuota
        {
            Id = id,
            TenantId = tenantId,
            LimitBytes = limitBytes,
            UsageBytes = 0,
            UpdatedAt = now,
        };
    }

    /// <summary>Identifier of the tenant the quota belongs to.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Explicit implementation preserves the <c>private set</c> DDD encapsulation on the
    /// public property while satisfying the interface contract used by Granit's tenant
    /// interceptor.
    /// </remarks>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Soft cap on the tenant's stored bytes. Enforcement runs in F7.2.</summary>
    public long LimitBytes { get; private set; }

    /// <summary>Sum of all active version <c>SizeBytes</c> rows for the tenant.</summary>
    public long UsageBytes { get; private set; }

    /// <summary>UTC instant of the last increment, decrement, or limit change.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Increments <see cref="UsageBytes"/> by <paramref name="delta"/>. Negative values
    /// are rejected — use <see cref="Decrement"/> for releases.
    /// </summary>
    /// <remarks>
    /// In production the service issues an atomic <c>UPDATE … SET UsageBytes = UsageBytes + @delta</c>;
    /// this method is the in-memory equivalent used by aggregate-level tests.
    /// </remarks>
    public void Increment(long delta, DateTimeOffset now)
    {
        if (delta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Increment must be non-negative.");
        }
        UsageBytes += delta;
        UpdatedAt = now;
    }

    /// <summary>
    /// Decrements <see cref="UsageBytes"/> by <paramref name="delta"/>. Clamps at zero
    /// to absorb any drift between the bookkeeping and the actual blob set — the F9.3
    /// recompute job is the authoritative reconciliation.
    /// </summary>
    public void Decrement(long delta, DateTimeOffset now)
    {
        if (delta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Decrement must be non-negative.");
        }
        UsageBytes = Math.Max(0, UsageBytes - delta);
        UpdatedAt = now;
    }

    /// <summary>Replaces the soft cap. Used by the F7.3 admin endpoint.</summary>
    public void SetLimit(long limitBytes, DateTimeOffset now)
    {
        if (limitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limitBytes), "Limit must be strictly positive.");
        }
        LimitBytes = limitBytes;
        UpdatedAt = now;
    }

    /// <summary>EF Core materialisation constructor.</summary>
    private TenantStorageQuota()
    {
    }
}
