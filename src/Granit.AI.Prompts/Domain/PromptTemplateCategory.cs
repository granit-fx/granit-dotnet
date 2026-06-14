using Granit.Domain;

namespace Granit.AI.Prompts.Domain;

/// <summary>
/// Link row joining a <see cref="PromptTemplate"/> to a <see cref="PromptCategory"/> (many-to-many).
/// Owned by the <see cref="PromptTemplate"/> aggregate; references the category by id.
/// </summary>
public sealed class PromptTemplateCategory : CreationAuditedEntity
{
    private PromptTemplateCategory()
    {
    }

    internal static PromptTemplateCategory Create(Guid id, Guid promptTemplateId, Guid categoryId) =>
        new() { Id = id, PromptTemplateId = promptTemplateId, CategoryId = categoryId };

    /// <summary>The linked prompt.</summary>
    public Guid PromptTemplateId { get; private set; }

    /// <summary>The linked category.</summary>
    public Guid CategoryId { get; private set; }
}
