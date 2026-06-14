using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.EntityFrameworkCore.Internal;
using Shouldly;

namespace Granit.AI.Prompts.EntityFrameworkCore.Tests;

public sealed class EfPromptCategoryStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfPromptCategoryStore _sut;

    public EfPromptCategoryStoreTests() => _sut = new EfPromptCategoryStore(_factory);

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Create_then_get_round_trips()
    {
        PromptCategory created = await _sut.CreateAsync(
            PromptCategory.Create(Guid.NewGuid(), "Marketing"), TestContext.Current.CancellationToken);

        PromptCategory? loaded = await _sut.GetAsync(created.Id, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("Marketing");
    }

    [Fact]
    public async Task GetByName_resolves_the_general_system_category()
    {
        await _sut.CreateAsync(
            PromptCategory.CreateSystem(Guid.NewGuid(), PromptCategory.GeneralName), TestContext.Current.CancellationToken);

        PromptCategory? general = await _sut.GetByNameAsync(PromptCategory.GeneralName, TestContext.Current.CancellationToken);

        general.ShouldNotBeNull();
        general.IsSystem.ShouldBeTrue();
    }

    [Fact]
    public async Task List_returns_categories_ordered_by_name()
    {
        await _sut.CreateAsync(PromptCategory.Create(Guid.NewGuid(), "Zeta"), TestContext.Current.CancellationToken);
        await _sut.CreateAsync(PromptCategory.Create(Guid.NewGuid(), "Alpha"), TestContext.Current.CancellationToken);

        IReadOnlyList<PromptCategory> all = await _sut.ListAsync(TestContext.Current.CancellationToken);

        all.Select(c => c.Name).ShouldBe(["Alpha", "Zeta"]);
    }
}
