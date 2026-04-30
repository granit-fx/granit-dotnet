namespace Granit.Entities.Views;

/// <summary>
/// Read-side service for the EntityView aggregate. Filters by the current user's
/// permissions: returns Personal views owned by the user, Shared views whose
/// audience targets the user, and all Tenant views in the current tenant.
/// </summary>
/// <remarks>
/// CQRS — see <see cref="IEntityViewWriter"/> for writes. Implementations live in
/// <c>Granit.Entities.Views.EntityFrameworkCore</c>.
/// </remarks>
public interface IEntityViewReader
{
    /// <summary>List every view accessible to the current user for the given entity.</summary>
    Task<IReadOnlyList<EntityViewDescriptor>> ListAsync(string entityName, CancellationToken cancellationToken = default);

    /// <summary>Get a single view by id, or <see langword="null"/> when missing or inaccessible to the current user.</summary>
    Task<EntityViewDescriptor?> GetAsync(string entityName, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve the user's effective default view for the entity per ADR-047 §4
    /// (precedence: <c>IsPersonalDefault</c> &gt; <c>IsDefault</c> &gt; compiled fallback).
    /// Returns <see langword="null"/> when the user has no saved or pinned view —
    /// the renderer falls back to the compiled <c>defaultCollection</c>.
    /// </summary>
    Task<EntityViewDescriptor?> GetDefaultViewAsync(string entityName, CancellationToken cancellationToken = default);
}
