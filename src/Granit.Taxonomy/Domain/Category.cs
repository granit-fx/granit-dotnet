using Granit.Domain;
using Granit.Taxonomy.Events;

namespace Granit.Taxonomy.Domain;

/// <summary>
/// Hierarchical category aggregate root — single-assignment classification per
/// ADR-054. Mirrors the <c>Folder</c> pattern from ADR-052: parent / child tree
/// with materialised path for fast subtree queries (e.g. "every category under
/// /electronics").
/// </summary>
/// <remarks>
/// Path convention: every category — root or child — has a <see cref="Path"/>
/// that starts with <c>"/"</c>, e.g. <c>"/electronics"</c> for a root and
/// <c>"/electronics/laptops"</c> for a child. Re-materialisation on move is
/// performed by <c>CategoryService.MoveAsync</c> with a single
/// <c>ExecuteUpdate</c>.
/// </remarks>
public sealed class Category : AggregateRoot, IMultiTenant
{
    /// <summary>Path component separator.</summary>
    public const string PathSeparator = "/";

    /// <summary>Maximum length, in characters, of <see cref="Path"/>.</summary>
    public const int MaxPathLength = 1024;

    /// <summary>Maximum length, in characters, of <see cref="Name"/>.</summary>
    public const int MaxNameLength = 100;

    /// <summary>Maximum length, in characters, of <see cref="Scope"/>.</summary>
    public const int MaxScopeLength = 64;

    /// <summary>Maximum length, in characters, of <see cref="IconName"/> (Lucide identifier).</summary>
    public const int MaxIconNameLength = 100;

    private Category() { }

    /// <summary>Creates a root category in <paramref name="scope"/>.</summary>
    public static Category CreateRoot(
        Guid id,
        Guid? tenantId,
        string scope,
        string name,
        string? iconName = null,
        bool hideOnEntityCard = false)
    {
        ValidateScope(scope);
        ValidateName(name);
        ValidateIconName(iconName);

        string path = PathSeparator + name;
        ValidatePathLength(path);

        var category = new Category
        {
            Id = id,
            TenantId = tenantId,
            Scope = scope,
            ParentId = null,
            Name = name,
            Path = path,
            Depth = 0,
            IconName = iconName,
            HideOnEntityCard = hideOnEntityCard,
            RowVersion = 1u,
        };
        category.AddDomainEvent(new CategoryCreatedEvent(
            category.Id, category.TenantId, category.Scope, category.ParentId,
            category.Name, category.Path, category.Depth));
        return category;
    }

    /// <summary>Creates a child category under <paramref name="parent"/>.</summary>
    public static Category Create(
        Guid id,
        Category parent,
        string name,
        string? iconName = null,
        bool hideOnEntityCard = false)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ValidateName(name);
        ValidateIconName(iconName);

        string path = ComputeChildPath(parent.Path, name);
        ValidatePathLength(path);

        var category = new Category
        {
            Id = id,
            TenantId = parent.TenantId,
            Scope = parent.Scope,
            ParentId = parent.Id,
            Name = name,
            Path = path,
            Depth = parent.Depth + 1,
            IconName = iconName,
            HideOnEntityCard = hideOnEntityCard,
            RowVersion = 1u,
        };
        category.AddDomainEvent(new CategoryCreatedEvent(
            category.Id, category.TenantId, category.Scope, category.ParentId,
            category.Name, category.Path, category.Depth));
        return category;
    }

    /// <summary>Identifier of the tenant that owns this category; <c>null</c> for host-global.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Domain scope (e.g. <c>"products"</c>, <c>"documents"</c>).</summary>
    public string Scope { get; private set; } = string.Empty;

    /// <summary>Identifier of the parent category, or <c>null</c> for a root.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>User-facing label.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Materialised path (<c>"/electronics/laptops"</c>). Always starts with
    /// <see cref="PathSeparator"/>; descendants of this category share the
    /// <c>Path + "/"</c> prefix.
    /// </summary>
    public string Path { get; private set; } = PathSeparator;

    /// <summary>Depth in the tree — 0 for roots, parent.Depth + 1 for children.</summary>
    public int Depth { get; private set; }

    /// <summary>Optional icon identifier (e.g. Lucide name).</summary>
    public string? IconName { get; private set; }

    /// <summary>When <c>true</c>, hides the category on entity cards while keeping it admin-visible.</summary>
    public bool HideOnEntityCard { get; private set; }

    /// <summary>Optimistic-concurrency token. Bumped on every state-mutating behavior.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>Renames the category. Descendant paths are re-materialised by the service layer.</summary>
    public void Rename(string newName)
    {
        ValidateName(newName);
        if (string.Equals(Name, newName, StringComparison.Ordinal))
        {
            return;
        }

        string oldPath = Path;
        string oldName = Name;

        // Rebuild own path under the same parent.
        string parentPath = oldPath[..^(oldName.Length + PathSeparator.Length)];
        if (parentPath.Length == 0)
        {
            parentPath = string.Empty; // root: "/electronics" → after strip → "" → child path is "/{newName}".
        }

        string newPath = parentPath.Length == 0
            ? PathSeparator + newName
            : ComputeChildPath(parentPath, newName);
        ValidatePathLength(newPath);

        Name = newName;
        Path = newPath;
        RowVersion++;
        AddDomainEvent(new CategoryRenamedEvent(Id, oldName, newName));
        AddDomainEvent(new CategoryTreePathChangedEvent(Id, TenantId, oldPath, newPath));
    }

    /// <summary>
    /// Moves the category under <paramref name="newParent"/>, or to root when
    /// <paramref name="newParent"/> is <c>null</c>. Same-tenant, same-scope, no-cycle
    /// validation is enforced; descendant paths are re-materialised by the service
    /// layer.
    /// </summary>
    public void MoveTo(Category? newParent)
    {
        if (newParent is not null)
        {
            if (newParent.TenantId != TenantId)
            {
                throw new InvalidOperationException("Cannot move a category to a different tenant.");
            }
            if (!string.Equals(newParent.Scope, Scope, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Cannot move a category to a different scope.");
            }
            if (newParent.Id == Id)
            {
                throw new InvalidOperationException("Cannot move a category under itself.");
            }
            if (IsAncestorOf(newParent))
            {
                throw new InvalidOperationException(
                    "Cannot move a category under one of its descendants (cycle).");
            }
            if (newParent.Id == ParentId)
            {
                return; // no-op
            }
        }
        else if (ParentId is null)
        {
            return; // already root, no-op
        }

        string oldPath = Path;
        int oldDepth = Depth;
        Guid? oldParentId = ParentId;

        ParentId = newParent?.Id;
        Path = newParent is null
            ? PathSeparator + Name
            : ComputeChildPath(newParent.Path, Name);
        Depth = (newParent?.Depth ?? -1) + 1;
        ValidatePathLength(Path);

        RowVersion++;
        AddDomainEvent(new CategoryMovedEvent(Id, oldParentId, ParentId));
        AddDomainEvent(new CategoryTreePathChangedEvent(Id, TenantId, oldPath, Path));
    }

    /// <summary>Replaces the icon. Pass <c>null</c> to clear it.</summary>
    public void SetIcon(string? iconName)
    {
        ValidateIconName(iconName);
        if (string.Equals(IconName ?? string.Empty, iconName ?? string.Empty, StringComparison.Ordinal))
        {
            return;
        }
        IconName = iconName;
        RowVersion++;
    }

    /// <summary>Flips the <see cref="HideOnEntityCard"/> flag.</summary>
    public void ToggleHideOnEntityCard()
    {
        HideOnEntityCard = !HideOnEntityCard;
        RowVersion++;
    }

    /// <summary>Marks the category as deleted by raising <see cref="CategoryDeletedEvent"/>.</summary>
    public void MarkDeleted() =>
        AddDomainEvent(new CategoryDeletedEvent(Id, TenantId, Scope, Path));

    private bool IsAncestorOf(Category candidate) =>
        candidate.Path == Path
        || candidate.Path.StartsWith(Path + PathSeparator, StringComparison.Ordinal);

    private static string ComputeChildPath(string parentPath, string childName) =>
        parentPath + PathSeparator + childName;

    private static void ValidateScope(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (scope.Length > MaxScopeLength)
        {
            throw new ArgumentException(
                $"Category scope exceeds {MaxScopeLength} characters.", nameof(scope));
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Category name exceeds {MaxNameLength} characters.", nameof(name));
        }
        if (name.Contains(PathSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Category name cannot contain the path separator '{PathSeparator}'.", nameof(name));
        }
    }

    private static void ValidateIconName(string? iconName)
    {
        if (iconName is { Length: > MaxIconNameLength })
        {
            throw new ArgumentException(
                $"Category icon name exceeds {MaxIconNameLength} characters.", nameof(iconName));
        }
    }

    private static void ValidatePathLength(string path)
    {
        if (path.Length > MaxPathLength)
        {
            throw new ArgumentException(
                $"Resulting category path exceeds {MaxPathLength} characters.");
        }
    }
}
