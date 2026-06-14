using Granit.AI.Prompts.Domain;

namespace Granit.AI.Prompts;

/// <summary>
/// Persistence for tenant-defined <see cref="PromptCategory"/> aggregates. Categories are shared
/// across the tenant (admin-defined); tenant isolation is applied by the underlying DbContext.
/// </summary>
public interface IPromptCategoryStore
{
    /// <summary>Persists a new category and returns it.</summary>
    Task<PromptCategory> CreateAsync(PromptCategory category, CancellationToken cancellationToken = default);

    /// <summary>Returns the category by id, or <see langword="null"/>.</summary>
    Task<PromptCategory?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the category with the given name, or <see langword="null"/>. Used to resolve the well-known "General".</summary>
    Task<PromptCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Returns all categories defined for the tenant, ordered by name.</summary>
    Task<IReadOnlyList<PromptCategory>> ListAsync(CancellationToken cancellationToken = default);
}
