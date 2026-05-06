using Granit.Taxonomy.Domain;

namespace Granit.Taxonomy;

/// <summary>
/// CRUD service for <see cref="Tag"/> aggregates.
/// </summary>
/// <remarks>
/// Phase T1 covers tag definition only — assignment to target entities ships in T2.2
/// via the polymorphic <c>TagAssignment</c> table and a separate
/// <c>ITagAssignmentService</c>.
/// </remarks>
public interface ITagService
{
    /// <summary>
    /// Creates a new tag in the current tenant. <paramref name="scope"/> is the domain
    /// discriminator (e.g. <c>"documents"</c>, <c>"global"</c>); <paramref name="name"/>
    /// the user-facing label; <paramref name="color"/> a hex string in
    /// <c>#RRGGBB</c> form; <paramref name="hideOnEntityCard"/> hides the tag from
    /// entity surfaces while keeping it available in admin / assignment forms.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a tag with the same <c>(TenantId, Scope, Name)</c> already exists.</exception>
    Task<Tag> CreateAsync(
        string scope,
        string name,
        string color,
        bool hideOnEntityCard = false,
        CancellationToken cancellationToken = default);

    /// <summary>Renames an existing tag.</summary>
    /// <returns>The updated tag, or <c>null</c> when no tag with <paramref name="id"/> exists.</returns>
    Task<Tag?> RenameAsync(
        Guid id,
        string newName,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces a tag's colour.</summary>
    /// <returns>The updated tag, or <c>null</c> when no tag with <paramref name="id"/> exists.</returns>
    Task<Tag?> RecolourAsync(
        Guid id,
        string newColor,
        CancellationToken cancellationToken = default);

    /// <summary>Toggles a tag's <c>HideOnEntityCard</c> flag.</summary>
    /// <returns>The updated tag, or <c>null</c> when no tag with <paramref name="id"/> exists.</returns>
    Task<Tag?> ToggleHideAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a tag. Orphan <c>TagAssignment</c> rows are cleaned up by T5.1.</summary>
    /// <returns><c>true</c> when the tag existed and was deleted; <c>false</c> otherwise.</returns>
    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a tag by id.</summary>
    Task<Tag?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists tags in <paramref name="scope"/> (pass <c>"*"</c> for cross-scope), optionally
    /// filtered by a case-insensitive name prefix <paramref name="q"/>. Pagination via
    /// <paramref name="skip"/> (default 0) and <paramref name="take"/> (default 50, max 500).
    /// </summary>
    Task<IReadOnlyList<Tag>> ListByScopeAsync(
        string scope,
        string? q = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);
}
