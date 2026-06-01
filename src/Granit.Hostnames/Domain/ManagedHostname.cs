using Granit.Domain;

namespace Granit.Hostnames.Domain;

/// <summary>
/// A hostname registered against an owning resource, for host-based routing. The owner is opaque —
/// <see cref="OwnerType"/> + <see cref="OwnerId"/> — so any consumer (CMS site, API gateway, billing
/// portal, …) can claim a hostname without this capability depending on it. <see cref="Host"/> is
/// globally unique, which gives anti-hijacking for free (a second owner cannot claim a taken host).
/// </summary>
/// <remarks>
/// In this foundation feature a hostname is <see cref="HostnameStatus.Active"/> on creation (trusted,
/// admin-registered — matching unverified domain lists). The verification feature introduces the
/// Pending → Verifying → Active state machine and the DNS challenge that gates <c>Active</c>.
/// </remarks>
public sealed class ManagedHostname : AuditedAggregateRoot, IMultiTenant, IConcurrencyAware
{
    private ManagedHostname()
    {
        // Required by EF Core materialisation.
    }

    /// <summary>The fully-qualified hostname (globally unique, lower-case).</summary>
    public Hostname Host { get; private set; } = null!;

    /// <summary>Opaque owner-resource discriminator (e.g. <c>"cms.site"</c>).</summary>
    public string OwnerType { get; private set; } = string.Empty;

    /// <summary>Identifier of the owning resource within <see cref="OwnerType"/>.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Owning tenant; <c>null</c> for a host-level (global) hostname.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Whether this is the canonical hostname among the owner's hostnames.</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>Lifecycle state. See <see cref="HostnameStatus"/>.</summary>
    public HostnameStatus Status { get; private set; } = HostnameStatus.Pending;

    /// <summary>Optimistic-concurrency token (ADR-061). Auto-managed by the framework interceptor.</summary>
    public string ConcurrencyStamp { get; private set; } = string.Empty;

    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    string IConcurrencyAware.ConcurrencyStamp
    {
        get => ConcurrencyStamp;
        set => ConcurrencyStamp = value;
    }

    /// <summary>Registers a hostname for an owning resource.</summary>
    /// <param name="id">Stable identifier (from <c>IGuidGenerator</c>).</param>
    /// <param name="host">The hostname (validated, lower-cased).</param>
    /// <param name="ownerType">Owner-resource discriminator (non-empty).</param>
    /// <param name="ownerId">Owning resource id.</param>
    /// <param name="tenantId">Owning tenant; <c>null</c> for a global hostname.</param>
    /// <param name="isPrimary">Whether this hostname is the owner's canonical one.</param>
    public static ManagedHostname Create(
        Guid id,
        Hostname host,
        string ownerType,
        Guid ownerId,
        Guid? tenantId = null,
        bool isPrimary = false)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerType);

        return new ManagedHostname
        {
            Id = id,
            Host = host,
            OwnerType = ownerType.Trim(),
            OwnerId = ownerId,
            TenantId = tenantId,
            IsPrimary = isPrimary,
            Status = HostnameStatus.Active,
        };
    }

    /// <summary>Marks this hostname as the owner's canonical one.</summary>
    public void SetPrimary() => IsPrimary = true;

    /// <summary>Clears the canonical flag.</summary>
    public void ClearPrimary() => IsPrimary = false;
}
