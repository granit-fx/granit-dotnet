using Granit.AI.Prompts.Domain;

namespace Granit.AI.Prompts;

/// <summary>
/// Persistence for <see cref="PromptTemplate"/> aggregates. The catalogue a user sees is the
/// framework-seeded system prompts plus their own private prompts (ADR-067, sharing deferred to
/// phase 2); reads are scoped accordingly. Tenant isolation is applied by the underlying DbContext.
/// </summary>
public interface IPromptTemplateStore
{
    /// <summary>Persists a new prompt and returns it.</summary>
    Task<PromptTemplate> CreateAsync(PromptTemplate prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the prompt if it is a system prompt or owned by <paramref name="ownerId"/>, otherwise
    /// <see langword="null"/> (another user's private prompt is reported as not found).
    /// </summary>
    Task<PromptTemplate?> GetAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Returns the owner's catalogue: system prompts plus their own, system-first then by name.</summary>
    Task<IReadOnlyList<PromptTemplate>> ListCatalogueAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies <paramref name="edit"/> to the owner's prompt (bumping its version) and reconciles its
    /// category membership. Returns the updated prompt, or <see langword="null"/> when no editable
    /// prompt with that id is owned by <paramref name="ownerId"/> (a system prompt is never editable —
    /// customise it to a private copy first).
    /// </summary>
    Task<PromptTemplate?> UpdateAsync(Guid id, Guid ownerId, PromptTemplateEdit edit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the owner's prompt. Returns <see langword="true"/> when a prompt was deleted, or
    /// <see langword="false"/> when no editable prompt with that id is owned by
    /// <paramref name="ownerId"/> (system prompts cannot be deleted).
    /// </summary>
    Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default);
}
