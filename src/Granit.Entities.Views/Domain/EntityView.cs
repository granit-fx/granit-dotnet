using System.Text.Json.Nodes;
using Granit.Domain;

namespace Granit.Entities.Views.Domain;

/// <summary>
/// Aggregate root for a saved view over a compiled collection (ADR-047).
/// </summary>
/// <remarks>
/// <para>
/// Immutable post-creation: <see cref="EntityName"/>, <see cref="BasedOn"/> and
/// <see cref="Kind"/> are fixed at <see cref="Create"/> time. The view's
/// <see cref="State"/> is a JSON delta over the compiled collection identified by
/// <see cref="BasedOn"/>; behavior methods mutate it explicitly.
/// </para>
/// <para>
/// Per CLAUDE.md DDD rules, all properties have private setters; mutations go through
/// behavior methods (<see cref="Rename"/>, <see cref="UpdateState"/>,
/// <see cref="SetPinned"/>, …). The <see cref="EntityView()"/> EF Core constructor is
/// reserved for materialization.
/// </para>
/// </remarks>
public sealed class EntityView : AuditedAggregateRoot, IMultiTenant
{
    /// <summary>Wire identifier of the entity this view targets (e.g. <c>"Granit.Parties.Party"</c>).</summary>
    public string EntityName { get; private set; } = null!;

    /// <summary>Name of the compiled collection this view deltas over (per ADR-042). <b>Immutable post-creation</b>.</summary>
    public string BasedOn { get; private set; } = null!;

    /// <summary>View kind inherited from <see cref="BasedOn"/>'s compiled collection. <b>Immutable post-creation</b>.</summary>
    public string Kind { get; private set; } = null!;

    /// <summary>User-facing label.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Optional description.</summary>
    public string? Description { get; private set; }

    /// <summary>Optional icon name.</summary>
    public string? Icon { get; private set; }

    /// <summary>JSONB delta over the base collection (filters, sort, columns, group, per-layout config).</summary>
    public JsonObject State { get; private set; } = null!;

    /// <summary>Visibility scope — see ADR-047 §4.</summary>
    public EntityViewVisibility Visibility { get; private set; }

    /// <summary>User identifier of the creator. <see langword="null"/> for Tenant views.</summary>
    public Guid? OwnerId { get; private set; }

    /// <summary>
    /// Audience for <see cref="EntityViewVisibility.Shared"/> views. The aggregate stores
    /// this via the <see cref="EntityViewSharedWith"/> value object; <see langword="null"/>
    /// for Personal / Tenant.
    /// </summary>
    public EntityViewSharedWith? SharedWith { get; private set; }

    /// <summary>Pinned as a tab in the workspace tab strip (admin-promoted).</summary>
    public bool IsPinned { get; private set; }

    /// <summary>Replaces the compiled default for the tenant (admin-promoted).</summary>
    public bool IsDefault { get; private set; }

    /// <summary>User's landing view when navigating to the entity (per-user). At most one per (user, entity).</summary>
    public bool IsPersonalDefault { get; private set; }

    /// <summary>Display order among views (lower first).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Tenant scoping (per <see cref="IMultiTenant"/>). <see langword="null"/> when host-scoped.</summary>
    public Guid? TenantId { get; private set; }

    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>EF Core materialization constructor — reserved.</summary>
    private EntityView() { }

    /// <summary>
    /// Factory: create a Personal view for the given owner. Personal is the only valid
    /// initial visibility — promotion to Shared / Tenant happens through
    /// <see cref="ShareWith"/> / <see cref="PromoteToTenant"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when any required string is empty / whitespace.</exception>
    public static EntityView Create(
        string entityName,
        string basedOn,
        string kind,
        string name,
        string? description,
        string? icon,
        JsonObject state,
        Guid ownerId,
        int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(basedOn);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(state);

        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("OwnerId must be non-empty for a Personal view.", nameof(ownerId));
        }

        return new EntityView
        {
            EntityName = entityName,
            BasedOn = basedOn,
            Kind = kind,
            Name = name,
            Description = description,
            Icon = icon,
            State = state,
            Visibility = EntityViewVisibility.Personal,
            OwnerId = ownerId,
            SharedWith = null,
            IsPinned = false,
            IsDefault = false,
            IsPersonalDefault = false,
            SortOrder = sortOrder,
        };
    }

    /// <summary>Rename the view + update its description / icon. Does not mutate <see cref="State"/>.</summary>
    public void Rename(string name, string? description, string? icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Description = description;
        Icon = icon;
    }

    /// <summary>Replace the JSON delta state. Caller must validate the payload against the schema for <see cref="Kind"/>.</summary>
    public void UpdateState(JsonObject state)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
    }

    /// <summary>Promote a Personal view to Shared with the given audience. Empty audience is rejected (the view would be unreachable).</summary>
    public void ShareWith(EntityViewSharedWith audience)
    {
        ArgumentNullException.ThrowIfNull(audience);

        if (Visibility == EntityViewVisibility.Tenant)
        {
            throw new InvalidOperationException("A Tenant view cannot be re-shared. Demote to Personal first.");
        }

        if (audience.Roles.Count == 0 && audience.Users.Count == 0)
        {
            throw new InvalidOperationException("ShareWith requires at least one role or user.");
        }

        Visibility = EntityViewVisibility.Shared;
        SharedWith = audience;
    }

    /// <summary>Promote a Personal / Shared view to Tenant. Clears <see cref="SharedWith"/> + <see cref="OwnerId"/>.</summary>
    public void PromoteToTenant()
    {
        Visibility = EntityViewVisibility.Tenant;
        OwnerId = null;
        SharedWith = null;
    }

    /// <summary>Set the pinned flag (admin promotion).</summary>
    public void SetPinned(bool isPinned) => IsPinned = isPinned;

    /// <summary>Set the tenant-default flag (admin promotion).</summary>
    public void SetTenantDefault(bool isDefault) => IsDefault = isDefault;

    /// <summary>Set the personal-default flag for the owner.</summary>
    public void SetPersonalDefault(bool isPersonalDefault) => IsPersonalDefault = isPersonalDefault;

    /// <summary>Set the display sort order.</summary>
    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
