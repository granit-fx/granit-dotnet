using Granit.Authorization.Events;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Authorization.Domain;

/// <summary>
/// Declarative metadata for a role: its name, tenant scope, owning OIDC client (if any),
/// and <see cref="Granit.MultiTenancy.MultiTenancySide"/>.
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
///   <item><see cref="MultiTenancySide.Host"/> ⇒ <see cref="TenantId"/> must be <see langword="null"/>.</item>
///   <item><see cref="MultiTenancySide.Both"/> ⇒ <see cref="TenantId"/> must be <see langword="null"/> (role is defined globally but assignable in any tenant context).</item>
///   <item><see cref="MultiTenancySide.Tenant"/> ⇒ <see cref="TenantId"/> must be non-null.</item>
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

    /// <summary>Tenant scope. Non-null only when <see cref="MultiTenancySide"/> is <see cref="MultiTenancySide.Tenant"/>.</summary>
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

    /// <summary>Host / tenant applicability side. Default on new declarations is <see cref="MultiTenancySide.Both"/>.</summary>
    public MultiTenancySide MultiTenancySide { get; private set; } = MultiTenancySide.Both;

    /// <summary>Optional description displayed in admin UIs. Max 2048 characters.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// <see langword="true"/> for roles provisioned by the platform seeder (e.g. <c>SuperAdmin</c>,
    /// <c>TenantAdministrator</c>, <c>User</c>). System roles cannot be renamed or deleted
    /// through the CRUD endpoints.
    /// </summary>
    public bool IsSystem { get; private set; }

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
    /// <param name="tenantId">Tenant scope. Must be non-null iff side is <see cref="MultiTenancySide.Tenant"/>.</param>
    /// <param name="clientId">Optional OIDC client scope. Max 256 chars.</param>
    /// <param name="description">Optional description. Max 2048 chars.</param>
    /// <param name="isSystem">Mark as seeded by the platform.</param>
    public static RoleMetadata Create(
        Guid id,
        string name,
        MultiTenancySide multiTenancySide,
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
            MultiTenancySide = multiTenancySide,
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

        Name = newName;
        Description = newDescription;

        AddDomainEvent(new RoleUpdatedEvent(Id, Name, MultiTenancySide, TenantId, ClientId));
    }

    /// <summary>
    /// Raises <see cref="RoleDeletedEvent"/> dispatched after commit. Call before removing
    /// the entity from the <c>DbContext</c> so the interceptor can collect the event from
    /// the still-tracked <c>Deleted</c> entry.
    /// </summary>
    public void MarkAsDeleted() =>
        AddDomainEvent(new RoleDeletedEvent(Id, Name, MultiTenancySide, TenantId, ClientId));

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

    private static void ValidateSideTenantConsistency(MultiTenancySide side, Guid? tenantId)
    {
        switch (side)
        {
            case MultiTenancySide.Host:
            case MultiTenancySide.Both:
                if (tenantId is not null)
                {
                    throw new ArgumentException(
                        $"Role with MultiTenancySide '{side}' must have a null TenantId.",
                        nameof(tenantId));
                }
                break;
            case MultiTenancySide.Tenant:
                if (tenantId is null)
                {
                    throw new ArgumentException(
                        "Tenant-scoped role requires a non-null TenantId.",
                        nameof(tenantId));
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(side), side, "Unknown MultiTenancySide value.");
        }
    }
}
