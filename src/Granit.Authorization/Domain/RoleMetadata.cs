using Granit.Authorization.Events;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Authorization.Domain;

/// <summary>
/// Declarative metadata for a role: its name, tenant scope, owning OIDC client (if any),
/// and <see cref="Granit.MultiTenancy.MultiTenancySides"/>.
/// </summary>
/// <remarks>
/// <para>
/// Symmetric to <see cref="PermissionDefinition"/>. While the underlying role lives in the
/// identity provider (local <c>GranitRole</c>, Keycloak realm role, Entra app role, etc.),
/// <see cref="RoleMetadata"/> holds the Granit-layer scoping attributes that the identity
/// provider does not model natively — primarily which side (Host / Tenant / Both) the role
/// applies to and which <see cref="Granit.MultiTenancy.ICurrentTenant"/> it belongs to when
/// scoped.
/// </para>
/// <para>
/// Invariants enforced by the <see cref="Create"/> factory and a matching database
/// <c>CHECK</c> constraint:
/// </para>
/// <list type="bullet">
///   <item><see cref="MultiTenancySides.Host"/> ⇒ <see cref="TenantId"/> must be <see langword="null"/>.</item>
///   <item><see cref="MultiTenancySides.Both"/> ⇒ <see cref="TenantId"/> must be <see langword="null"/> (role is defined globally but assignable in any tenant context).</item>
///   <item><see cref="MultiTenancySides.Tenant"/> ⇒ <see cref="TenantId"/> must be non-null.</item>
/// </list>
/// <para>
/// Uniqueness is enforced on the composite <c>(Name, TenantId, ClientId)</c> with
/// <c>NULLS NOT DISTINCT</c> semantics (PostgreSQL 15+) so host-scope rows with
/// <c>TenantId = null</c> and <c>ClientId = null</c> cannot duplicate each other.
/// </para>
/// </remarks>
public sealed class RoleMetadata : AuditedAggregateRoot, IMultiTenant
{
    /// <summary>Human-readable role name, e.g. <c>"TenantAdministrator"</c>. Max 256 characters.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Tenant scope. Non-null only when <see cref="MultiTenancySides"/> is <see cref="MultiTenancySides.Tenant"/>.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// OIDC client identifier the role is scoped to (<c>null</c> for realm / global roles).
    /// Max 256 characters.
    /// </summary>
    /// <remarks>
    /// Reserved for a future realm vs client role distinction on federated providers
    /// (Keycloak client roles, Entra app roles, etc.). Locally created roles leave this
    /// <see langword="null"/>.
    /// </remarks>
    public string? ClientId { get; private set; }

    /// <summary>Host / tenant applicability side. Default on new declarations is <see cref="MultiTenancySides.Both"/>.</summary>
    public MultiTenancySides MultiTenancySides { get; private set; } = MultiTenancySides.Both;

    /// <summary>Optional description displayed in admin UIs. Max 2048 characters.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// <see langword="true"/> for roles provisioned by the platform seeder (e.g. <c>SuperAdmin</c>,
    /// <c>TenantAdministrator</c>, <c>User</c>). System roles cannot be renamed or deleted
    /// through the CRUD endpoints.
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>
    /// <see langword="true"/> when the client-role sync saw this row in a previous pass but
    /// the upstream provider no longer returns it — only flipped by the
    /// <see cref="OrphanedRolePolicy.SoftDelete"/> policy. Hard-deleted rows do not carry
    /// this flag (they are physically removed).
    /// </summary>
    /// <remarks>
    /// <c>FindByNameAsync</c> does <b>not</b> filter on this flag — orphaned rows keep
    /// resolving so existing <c>PermissionGrant</c> entries continue to work until an
    /// admin curates them. See ADR-029.
    /// </remarks>
    public bool IsOrphaned { get; private set; }

    /// <summary>
    /// Timestamp captured by the sync when <see cref="IsOrphaned"/> was flipped.
    /// <see langword="null"/> on rows that were never orphaned, and on rows that have been
    /// restored (<see cref="RestoreFromOrphaned"/> clears the stamp).
    /// </summary>
    public DateTimeOffset? OrphanedAt { get; private set; }

    /// <summary>Required by EF Core materialization — never call from application code.</summary>
    private RoleMetadata() { }

    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>
    /// Creates a new <see cref="RoleMetadata"/> and raises a <see cref="RoleCreatedEvent"/>
    /// to be dispatched after the transaction commits.
    /// </summary>
    /// <param name="id">Aggregate identifier (typically supplied by <c>IGuidGenerator</c>).</param>
    /// <param name="name">Role name. Max 256 chars, non-empty.</param>
    /// <param name="multiTenancySide">Side applicability.</param>
    /// <param name="tenantId">Tenant scope. Must be non-null iff side is <see cref="MultiTenancySides.Tenant"/>.</param>
    /// <param name="clientId">Optional OIDC client scope. Max 256 chars.</param>
    /// <param name="description">Optional description. Max 2048 chars.</param>
    /// <param name="isSystem">Mark as seeded by the platform.</param>
    public static RoleMetadata Create(
        Guid id,
        string name,
        MultiTenancySides multiTenancySide,
        Guid? tenantId,
        string? clientId = null,
        string? description = null,
        bool isSystem = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateLengths(name, clientId, description);
        ValidateSideTenantConsistency(multiTenancySide, tenantId);

        RoleMetadata role = new()
        {
            Id = id,
            Name = name,
            MultiTenancySides = multiTenancySide,
            TenantId = tenantId,
            ClientId = clientId,
            Description = description,
            IsSystem = isSystem,
        };

        role.AddDomainEvent(new RoleCreatedEvent(
            id, name, multiTenancySide, tenantId, clientId));

        return role;
    }

    /// <summary>
    /// Renames the role and / or updates its description. Raises <see cref="RoleUpdatedEvent"/>
    /// on any actual change. No-op if no property changes.
    /// </summary>
    /// <remarks>
    /// Side and tenant scope are immutable after creation to preserve the domain invariants
    /// and the uniqueness index. Change the scope by deleting and re-creating the role.
    /// </remarks>
    public void Rename(string newName, string? newDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        ValidateLengths(newName, ClientId, newDescription);

        bool nameChanged = !string.Equals(newName, Name, StringComparison.Ordinal);
        bool descriptionChanged = !string.Equals(newDescription, Description, StringComparison.Ordinal);

        if (!nameChanged && !descriptionChanged)
        {
            return;
        }

        string? previousName = nameChanged ? Name : null;

        Name = newName;
        Description = newDescription;

        AddDomainEvent(new RoleUpdatedEvent(
            Id, Name, previousName, MultiTenancySides, TenantId, ClientId));
    }

    /// <summary>
    /// Raises <see cref="RoleDeletedEvent"/> dispatched after commit. Call before removing
    /// the entity from the <c>DbContext</c> so the interceptor can collect the event from
    /// the still-tracked <c>Deleted</c> entry.
    /// </summary>
    public void MarkAsDeleted() =>
        AddDomainEvent(new RoleDeletedEvent(Id, Name, MultiTenancySides, TenantId, ClientId));

    /// <summary>
    /// Flips <see cref="IsOrphaned"/> to <see langword="true"/>, stamps <see cref="OrphanedAt"/>,
    /// and raises <see cref="RoleOrphanedEvent"/>. Idempotent — if already orphaned, no state
    /// change and no event. Called by the client-role sync under the
    /// <see cref="OrphanedRolePolicy.SoftDelete"/> policy.
    /// </summary>
    /// <param name="now">Timestamp captured by the sync (typically <c>IClock.Now</c>).</param>
    public void MarkAsOrphaned(DateTimeOffset now)
    {
        if (IsOrphaned)
        {
            return;
        }

        IsOrphaned = true;
        OrphanedAt = now;

        AddDomainEvent(new RoleOrphanedEvent(
            Id, Name, MultiTenancySides, TenantId, ClientId, now));
    }

    /// <summary>
    /// Clears the <see cref="IsOrphaned"/> flag and the <see cref="OrphanedAt"/> stamp,
    /// raising <see cref="RoleRestoredEvent"/>. Idempotent — no state change and no event
    /// when the row was not orphaned. Called by the sync when an upstream role that was
    /// previously marked orphaned starts being returned again (admin re-added it).
    /// </summary>
    public void RestoreFromOrphaned()
    {
        if (!IsOrphaned)
        {
            return;
        }

        IsOrphaned = false;
        OrphanedAt = null;

        AddDomainEvent(new RoleRestoredEvent(
            Id, Name, MultiTenancySides, TenantId, ClientId));
    }

    private static void ValidateLengths(string name, string? clientId, string? description)
    {
        if (name.Length > 256)
        {
            throw new ArgumentException("Role name exceeds 256 characters.", nameof(name));
        }

        if (clientId is { Length: > 256 })
        {
            throw new ArgumentException("Client id exceeds 256 characters.", nameof(clientId));
        }

        if (description is { Length: > 2048 })
        {
            throw new ArgumentException("Description exceeds 2048 characters.", nameof(description));
        }
    }

    private static void ValidateSideTenantConsistency(MultiTenancySides side, Guid? tenantId)
    {
        switch (side)
        {
            case MultiTenancySides.Host:
            case MultiTenancySides.Both:
                if (tenantId is not null)
                {
                    throw new ArgumentException(
                        $"Role with MultiTenancySides '{side}' must have a null TenantId.",
                        nameof(tenantId));
                }
                break;
            case MultiTenancySides.Tenant:
                if (tenantId is null)
                {
                    throw new ArgumentException(
                        "Tenant-scoped role requires a non-null TenantId.",
                        nameof(tenantId));
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(side), side, "Unknown MultiTenancySides value.");
        }
    }
}
