using Granit.AI.Prompts.Domain;

namespace Granit.AI.Prompts;

/// <summary>
/// Privacy and data-lifecycle operations over <see cref="PromptTemplate"/> aggregates that cross the
/// owner-scoped boundary of <see cref="IPromptTemplateStore"/> (ADR-067, GDPR). Used by the
/// <c>Granit.AI.Prompts.Privacy</c> export/erasure handlers — never by ordinary request handlers.
/// System prompts (framework seeds, owned by no user) are never a data subject's personal data and
/// are excluded from every operation here.
/// </summary>
public interface IPromptTemplateDataManager
{
    /// <summary>
    /// Returns every user-owned prompt (excluding system prompts) owned by <paramref name="ownerId"/>,
    /// for a data take-out. Respects the ambient tenant scope.
    /// </summary>
    Task<IReadOnlyList<PromptTemplate>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently erases (hard delete, bypassing soft-delete) every user-owned prompt (and its
    /// category links) owned by <paramref name="ownerId"/>, optionally constrained to
    /// <paramref name="tenantId"/>. System prompts are never touched. Returns the number of prompts
    /// removed. Idempotent.
    /// </summary>
    Task<int> EraseOwnerAsync(Guid? tenantId, Guid ownerId, CancellationToken cancellationToken = default);
}
