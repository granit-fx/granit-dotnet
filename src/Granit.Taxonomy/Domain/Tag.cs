using System.Text.RegularExpressions;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Taxonomy.Events;

namespace Granit.Taxonomy.Domain;

/// <summary>
/// Aggregate root for a tenant-defined tag — a flat label assignable to any aggregate
/// root through the polymorphic <c>TagAssignment</c> table introduced in T2.2.
/// </summary>
/// <remarks>
/// <para>
/// Tags are <b>scoped</b>: a <c>Scope</c> discriminator (e.g. <c>"documents"</c>,
/// <c>"parties"</c>, <c>"global"</c>) groups tags per consuming domain so each module's
/// autocomplete stays focused. A single physical table still enables cross-entity
/// search via <c>?scope=*</c>. See ADR-054 for the full design.
/// </para>
/// <para>
/// Uniqueness is enforced on <c>(TenantId, Scope, Name)</c>; the same name is allowed
/// across scopes (a "VIP" tag in <c>parties</c> and another "VIP" in <c>documents</c>
/// coexist without conflict).
/// </para>
/// <para>
/// <see cref="HideOnEntityCard"/> generalises Odoo's <c>hide_in_kanban</c> flag: an
/// operational tag remains visible in admin UIs and assignment forms (so it can be
/// applied) but is suppressed from entity card / list / detail surfaces.
/// </para>
/// <para>
/// <see cref="RowVersion"/> is a manually-incremented optimistic-concurrency token
/// (portable across SQL Server, PostgreSQL, and SQLite). Every behavior method that
/// mutates state increments it.
/// </para>
/// </remarks>
public sealed partial class Tag : AggregateRoot, IMultiTenant
{
    /// <summary>Maximum length, in characters, of a tag <see cref="Name"/>.</summary>
    public const int MaxNameLength = 50;

    /// <summary>Maximum length, in characters, of a tag <see cref="Scope"/>.</summary>
    public const int MaxScopeLength = 64;

    /// <summary>Length, in characters, of a hex <see cref="Color"/> (<c>#RRGGBB</c>).</summary>
    public const int ColorLength = 7;

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex HexColorRegex();

    private Tag() { }

    /// <summary>
    /// Creates a new tag in <paramref name="scope"/> for the given tenant.
    /// </summary>
    /// <param name="id">Unique identifier of the new tag.</param>
    /// <param name="tenantId">Owning tenant; <c>null</c> for host-global tags.</param>
    /// <param name="scope">Domain scope (e.g. <c>"documents"</c>, <c>"global"</c>).</param>
    /// <param name="name">User-facing label (max 50 chars).</param>
    /// <param name="color">Hex colour (<c>#RRGGBB</c>).</param>
    /// <param name="hideOnEntityCard">When <c>true</c>, hides the tag from entity surfaces; default <c>false</c>.</param>
    public static Tag Create(
        Guid id,
        Guid? tenantId,
        string scope,
        string name,
        string color,
        bool hideOnEntityCard = false)
    {
        ValidateScope(scope);
        ValidateName(name);
        ValidateColor(color);

        Tag tag = new()
        {
            Id = id,
            TenantId = tenantId,
            Scope = scope,
            Name = name,
            Color = color,
            HideOnEntityCard = hideOnEntityCard,
            RowVersion = 1u,
        };
        tag.AddDomainEvent(new TagCreatedEvent(
            tag.Id, tag.TenantId, tag.Scope, tag.Name, tag.Color, tag.HideOnEntityCard));
        return tag;
    }

    /// <summary>Identifier of the tenant that owns this tag; <c>null</c> for host-global tags.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Domain scope (e.g. <c>"documents"</c>, <c>"parties"</c>, <c>"global"</c>).</summary>
    public string Scope { get; private set; } = string.Empty;

    /// <summary>User-facing label.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Hex colour (<c>#RRGGBB</c>) used in admin UIs and badges.</summary>
    public string Color { get; private set; } = string.Empty;

    /// <summary>
    /// When <c>true</c>, the tag is visible in admin / assignment surfaces but hidden on
    /// the entity card / list / detail views.
    /// </summary>
    public bool HideOnEntityCard { get; private set; }

    /// <summary>Optimistic-concurrency token. Manually incremented on each behavior method.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>Renames the tag.</summary>
    public void Rename(string newName)
    {
        ValidateName(newName);
        if (string.Equals(Name, newName, StringComparison.Ordinal))
        {
            return;
        }
        string oldName = Name;
        Name = newName;
        RowVersion++;
        AddDomainEvent(new TagRenamedEvent(Id, oldName, newName));
    }

    /// <summary>Replaces the colour.</summary>
    public void Recolour(string newColor)
    {
        ValidateColor(newColor);
        if (string.Equals(Color, newColor, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        string oldColor = Color;
        Color = newColor;
        RowVersion++;
        AddDomainEvent(new TagRecolouredEvent(Id, oldColor, newColor));
    }

    /// <summary>Flips the <see cref="HideOnEntityCard"/> flag.</summary>
    public void ToggleHideOnEntityCard()
    {
        HideOnEntityCard = !HideOnEntityCard;
        RowVersion++;
        AddDomainEvent(new TagHideOnEntityCardChangedEvent(Id, HideOnEntityCard));
    }

    /// <summary>
    /// Marks the tag as deleted by raising <see cref="TagDeletedEvent"/>. The persistence
    /// layer hard-deletes the row; the cleanup of orphan <c>TagAssignment</c> rows is
    /// handled by the <c>EntityDeletedEto</c> listener (T5.1).
    /// </summary>
    public void MarkDeleted() =>
        AddDomainEvent(new TagDeletedEvent(Id, TenantId, Scope, Name));

    private static void ValidateScope(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (scope.Length > MaxScopeLength)
        {
            throw new ArgumentException(
                $"Tag scope exceeds {MaxScopeLength} characters.", nameof(scope));
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Tag name exceeds {MaxNameLength} characters.", nameof(name));
        }
    }

    private static void ValidateColor(string color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(color);
        if (!HexColorRegex().IsMatch(color))
        {
            throw new ArgumentException(
                "Tag colour must be a hex string in the form '#RRGGBB'.", nameof(color));
        }
    }
}
