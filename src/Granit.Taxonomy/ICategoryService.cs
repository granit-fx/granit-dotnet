using Granit.Taxonomy.Domain;

namespace Granit.Taxonomy;

/// <summary>CRUD service for the hierarchical <see cref="Category"/> aggregate.</summary>
public interface ICategoryService
{
    /// <summary>Creates a new category. Pass <paramref name="parentId"/> = <c>null</c> for a root.</summary>
    Task<Category> CreateAsync(
        string scope,
        Guid? parentId,
        string name,
        string? iconName = null,
        bool hideOnEntityCard = false,
        CancellationToken cancellationToken = default);

    /// <summary>Renames a category. Descendant paths are re-materialised in a single SQL UPDATE.</summary>
    Task<Category?> RenameAsync(
        Guid id,
        string newName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a category under <paramref name="newParentId"/>, or to root when
    /// <c>null</c>. Descendant paths and depths are re-materialised in a single
    /// SQL UPDATE.
    /// </summary>
    Task<Category?> MoveAsync(
        Guid id,
        Guid? newParentId,
        CancellationToken cancellationToken = default);

    /// <summary>Sets or clears the icon.</summary>
    Task<Category?> SetIconAsync(
        Guid id,
        string? iconName,
        CancellationToken cancellationToken = default);

    /// <summary>Toggles the <c>HideOnEntityCard</c> flag.</summary>
    Task<Category?> ToggleHideAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-deletes a category. Throws when descendants exist — callers must move /
    /// delete descendants first, or use a future <c>DeleteSubtreeAsync</c> overload.
    /// </summary>
    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a category by id.</summary>
    Task<Category?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Lists every category in <paramref name="scope"/>, ordered by path.</summary>
    Task<IReadOnlyList<Category>> ListByScopeAsync(
        string scope,
        CancellationToken cancellationToken = default);
}
