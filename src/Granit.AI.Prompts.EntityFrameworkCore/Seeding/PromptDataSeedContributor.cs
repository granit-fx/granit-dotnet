using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.EntityFrameworkCore.Internal;
using Granit.AI.Prompts.Seeding;
using Granit.Domain.ValueObjects;
using Granit.Guids;
using Granit.Persistence.DataSeeding;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore.Seeding;

/// <summary>
/// Seeds the framework generic prompts (<see cref="GenericPrompts"/>) and the well-known
/// <see cref="PromptCategory.GeneralName"/> category into every tenant's catalogue (ADR-067).
/// Idempotent: a system prompt is matched by its stable name key; created when missing and updated
/// (version-bumped) only when the framework definition changed. User copies (<c>IsSystem == false</c>)
/// are never touched.
/// </summary>
internal sealed class PromptDataSeedContributor(
    IDbContextFactory<AIPromptsDbContext> contextFactory,
    IGuidGenerator guidGenerator) : ITenantDataSeedContributor
{
    public async Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using AIPromptsDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        PromptCategory general = await EnsureGeneralCategoryAsync(db, cancellationToken).ConfigureAwait(false);

        foreach (GenericPromptSeed seed in GenericPrompts.All)
        {
            await UpsertSeedAsync(db, seed, general.Id, cancellationToken).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<PromptCategory> EnsureGeneralCategoryAsync(AIPromptsDbContext db, CancellationToken cancellationToken)
    {
        PromptCategory? general = await db.PromptCategories
            .FirstOrDefaultAsync(c => c.IsSystem && c.Name == PromptCategory.GeneralName, cancellationToken)
            .ConfigureAwait(false);
        if (general is not null)
        {
            return general;
        }

        general = PromptCategory.CreateSystem(guidGenerator.Create(), PromptCategory.GeneralName);
        db.PromptCategories.Add(general);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return general;
    }

    private async Task UpsertSeedAsync(
        AIPromptsDbContext db, GenericPromptSeed seed, Guid generalCategoryId, CancellationToken cancellationToken)
    {
        PromptTemplate? existing = await db.PromptTemplates
            .Include(p => p.CategoryLinks)
            .FirstOrDefaultAsync(p => p.IsSystem && p.Name == seed.NameKey, cancellationToken)
            .ConfigureAwait(false);

        HexColor color = seed.IconColor;

        if (existing is null)
        {
            var prompt = PromptTemplate.CreateSystem(
                guidGenerator.Create(), seed.NameKey, seed.DescriptionKey, seed.Content, seed.Icon, color);
            prompt.AssignCategory(guidGenerator.Create(), generalCategoryId);
            db.PromptTemplates.Add(prompt);
            return;
        }

        // Push framework changes only when something actually differs — keeps re-seeding idempotent.
        if (existing.ShortDescription != seed.DescriptionKey
            || existing.Content != seed.Content
            || existing.Icon != seed.Icon
            || existing.IconColor?.Value != color.Value)
        {
            existing.Edit(seed.NameKey, seed.DescriptionKey, seed.Content, seed.Icon, color);
        }

        existing.AssignCategory(guidGenerator.Create(), generalCategoryId);
    }
}
