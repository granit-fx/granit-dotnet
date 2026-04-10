namespace Granit.Persistence.EntityFrameworkCore.DataSeeding;

/// <summary>
/// Context passed to each <see cref="IDataSeedContributor"/> during seeding.
/// Contains the optional tenant identifier and a property bag for contributor-specific data.
/// </summary>
/// <remarks>
/// <para>
/// When the application runs in a multi-tenant topology, <see cref="TenantId"/> identifies
/// the tenant being seeded. Contributors can use this to seed tenant-specific reference data.
/// A <c>null</c> value indicates the host-level (shared) context.
/// </para>
/// <para>
/// The <see cref="Properties"/> dictionary allows callers to pass arbitrary data to contributors
/// (e.g., an admin email address for initial user creation).
/// </para>
/// </remarks>
public sealed class DataSeedContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="DataSeedContext"/>.
    /// </summary>
    /// <param name="tenantId">
    /// Identifier of the tenant being seeded, or <c>null</c> for host-level seeding.
    /// </param>
    public DataSeedContext(Guid? tenantId = null)
    {
        TenantId = tenantId;
    }

    /// <summary>
    /// Well-known property key for host-only seeding mode.
    /// When <c>true</c>, tenant-scoped seed contributors should skip their work
    /// (tenant tables don't exist yet during the first seed pass).
    /// </summary>
    [Obsolete("Use IHostDataSeedContributor / ITenantDataSeedContributor instead of checking IsHostOnly. " +
              "Kept for backward compatibility with legacy IDataSeedContributor implementations.")]
    public const string HostOnlyKey = "Granit:HostOnly";

    /// <summary>
    /// Identifier of the tenant being seeded, or <c>null</c> for host-level seeding.
    /// </summary>
    public Guid? TenantId { get; }

    /// <summary>
    /// Indicates whether this is a host-only seed pass (first pass in SchemaPerTenant mode).
    /// Tenant-scoped seed contributors should return early when this is <c>true</c>.
    /// </summary>
    [Obsolete("Use IHostDataSeedContributor / ITenantDataSeedContributor instead of checking IsHostOnly. " +
              "Kept for backward compatibility with legacy IDataSeedContributor implementations.")]
    public bool IsHostOnly => this[HostOnlyKey] is true;

    /// <summary>
    /// Arbitrary key-value pairs for passing data to seed contributors.
    /// </summary>
    public Dictionary<string, object?> Properties { get; } = [];

    /// <summary>
    /// Gets or sets a property value by key.
    /// </summary>
    /// <param name="key">The property key.</param>
    /// <returns>The value, or <c>null</c> if the key is not found.</returns>
    public object? this[string key]
    {
        get => Properties.GetValueOrDefault(key);
        set => Properties[key] = value;
    }
}
