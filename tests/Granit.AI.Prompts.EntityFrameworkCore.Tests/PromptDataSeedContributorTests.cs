using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.EntityFrameworkCore.Internal;
using Granit.AI.Prompts.EntityFrameworkCore.Seeding;
using Granit.AI.Prompts.Seeding;
using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Granit.AI.Prompts.EntityFrameworkCore.Tests;

public sealed class PromptDataSeedContributorTests : IDisposable
{
    private sealed class FakeGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly PromptDataSeedContributor _sut;

    public PromptDataSeedContributorTests() => _sut = new PromptDataSeedContributor(_factory, new FakeGuidGenerator());

    public void Dispose() => _factory.Dispose();

    private async Task<(int Categories, int Prompts)> CountAsync()
    {
        await using AIPromptsDbContext db = _factory.CreateDbContext();
        int categories = await db.PromptCategories.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        int prompts = await db.PromptTemplates.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        return (categories, prompts);
    }

    [Fact]
    public async Task Seeds_the_general_category_and_the_generic_prompts_under_it()
    {
        await _sut.SeedAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        await using AIPromptsDbContext db = _factory.CreateDbContext();
        PromptCategory general = await db.PromptCategories
            .SingleAsync(c => c.IsSystem && c.Name == PromptCategory.GeneralName, TestContext.Current.CancellationToken);

        List<PromptTemplate> systemPrompts = await db.PromptTemplates
            .Include(p => p.CategoryLinks)
            .Where(p => p.IsSystem)
            .ToListAsync(TestContext.Current.CancellationToken);

        systemPrompts.Count.ShouldBe(GenericPrompts.All.Count);
        systemPrompts.ShouldAllBe(p => p.CategoryLinks.Any(l => l.CategoryId == general.Id));
        systemPrompts.ShouldContain(p => p.Name == "Prompt:Summarize:Name");
    }

    [Fact]
    public async Task Re_seeding_is_idempotent_and_does_not_duplicate_or_bump_versions()
    {
        await _sut.SeedAsync(new DataSeedContext(), TestContext.Current.CancellationToken);
        (int catsFirst, int promptsFirst) = await CountAsync();

        await _sut.SeedAsync(new DataSeedContext(), TestContext.Current.CancellationToken);
        (int catsSecond, int promptsSecond) = await CountAsync();

        catsSecond.ShouldBe(catsFirst);
        promptsSecond.ShouldBe(promptsFirst);

        await using AIPromptsDbContext db = _factory.CreateDbContext();
        // No spurious edits on identical re-seed → version stays at 1.
        (await db.PromptTemplates.Where(p => p.IsSystem).ToListAsync(TestContext.Current.CancellationToken))
            .ShouldAllBe(p => p.Version == 1);
    }

    [Fact]
    public async Task Re_seeding_does_not_touch_user_copies()
    {
        await _sut.SeedAsync(new DataSeedContext(), TestContext.Current.CancellationToken);
        var userPromptId = Guid.NewGuid();
        await using (AIPromptsDbContext db = _factory.CreateDbContext())
        {
            db.PromptTemplates.Add(PromptTemplate.Create(userPromptId, Guid.NewGuid(), "My copy", "mine", "do it"));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await _sut.SeedAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        await using AIPromptsDbContext verify = _factory.CreateDbContext();
        PromptTemplate userCopy = await verify.PromptTemplates.SingleAsync(p => p.Id == userPromptId, TestContext.Current.CancellationToken);
        userCopy.Name.ShouldBe("My copy");
        userCopy.IsSystem.ShouldBeFalse();
        userCopy.Version.ShouldBe(1);
    }
}
