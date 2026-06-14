using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.EntityFrameworkCore.Internal;
using Granit.Guids;
using Shouldly;

namespace Granit.AI.Prompts.EntityFrameworkCore.Tests;

public sealed class EfPromptTemplateStoreTests : IDisposable
{
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfPromptTemplateStore _sut;

    public EfPromptTemplateStoreTests() => _sut = new EfPromptTemplateStore(_factory, new SimpleGuidGenerator());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Create_then_get_round_trips_a_user_prompt()
    {
        PromptTemplate created = await _sut.CreateAsync(
            PromptTemplate.Create(Guid.NewGuid(), UserA, "Draft", "Draft text", "Draft: {x}", "pencil", "#10B981"),
            TestContext.Current.CancellationToken);

        PromptTemplate? loaded = await _sut.GetAsync(created.Id, UserA, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("Draft");
        loaded.IconColor!.Value.ShouldBe("#10B981");
    }

    [Fact]
    public async Task Get_does_not_return_another_users_private_prompt()
    {
        PromptTemplate created = await _sut.CreateAsync(
            PromptTemplate.Create(Guid.NewGuid(), UserA, "Private", "x", "y"), TestContext.Current.CancellationToken);

        (await _sut.GetAsync(created.Id, UserB, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Get_returns_a_system_prompt_to_any_user()
    {
        PromptTemplate system = await _sut.CreateAsync(
            PromptTemplate.CreateSystem(Guid.NewGuid(), "Daily brief", "x", "brief"), TestContext.Current.CancellationToken);

        (await _sut.GetAsync(system.Id, UserB, TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_prompt_assigned_to_several_categories_round_trips_all_its_links()
    {
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        var prompt = PromptTemplate.Create(Guid.NewGuid(), UserA, "Multi", "x", "y");
        prompt.AssignCategory(Guid.NewGuid(), catA);
        prompt.AssignCategory(Guid.NewGuid(), catB);
        await _sut.CreateAsync(prompt, TestContext.Current.CancellationToken);

        PromptTemplate? loaded = await _sut.GetAsync(prompt.Id, UserA, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.CategoryLinks.Select(l => l.CategoryId).ShouldBe([catA, catB], ignoreOrder: true);
    }

    [Fact]
    public async Task Catalogue_lists_system_prompts_first_then_the_owners_excluding_other_users()
    {
        await _sut.CreateAsync(PromptTemplate.CreateSystem(Guid.NewGuid(), "Zeta system", "x", "z"), TestContext.Current.CancellationToken);
        await _sut.CreateAsync(PromptTemplate.Create(Guid.NewGuid(), UserA, "Alpha mine", "x", "a"), TestContext.Current.CancellationToken);
        await _sut.CreateAsync(PromptTemplate.Create(Guid.NewGuid(), UserB, "Other private", "x", "o"), TestContext.Current.CancellationToken);

        IReadOnlyList<PromptTemplate> catalogue = await _sut.ListCatalogueAsync(UserA, TestContext.Current.CancellationToken);

        catalogue.Select(p => p.Name).ShouldBe(["Zeta system", "Alpha mine"]); // system first, then owner's
        catalogue.ShouldNotContain(p => p.Name == "Other private");
    }

    [Fact]
    public async Task Update_bumps_the_version_and_reconciles_categories()
    {
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        var prompt = PromptTemplate.Create(Guid.NewGuid(), UserA, "Before", "x", "y");
        prompt.AssignCategory(Guid.NewGuid(), catA);
        await _sut.CreateAsync(prompt, TestContext.Current.CancellationToken);

        PromptTemplate? updated = await _sut.UpdateAsync(
            prompt.Id, UserA,
            new PromptTemplateEdit("After", "new desc", "new content", "star", "#FF0000", [catB]),
            TestContext.Current.CancellationToken);

        updated.ShouldNotBeNull();
        updated.Name.ShouldBe("After");
        updated.Version.ShouldBe(2);
        updated.CategoryLinks.Select(l => l.CategoryId).ShouldBe([catB]);
    }

    [Fact]
    public async Task Update_returns_null_for_a_system_prompt_or_another_users_prompt()
    {
        PromptTemplate system = await _sut.CreateAsync(
            PromptTemplate.CreateSystem(Guid.NewGuid(), "Prompt:Summarize:Name", "x", "z"), TestContext.Current.CancellationToken);
        PromptTemplate other = await _sut.CreateAsync(
            PromptTemplate.Create(Guid.NewGuid(), UserB, "Theirs", "x", "y"), TestContext.Current.CancellationToken);

        var edit = new PromptTemplateEdit("Hijack", "x", "y", null, null, []);

        (await _sut.UpdateAsync(system.Id, UserA, edit, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await _sut.UpdateAsync(other.Id, UserA, edit, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Delete_removes_an_owned_prompt_but_not_a_system_or_foreign_one()
    {
        PromptTemplate mine = await _sut.CreateAsync(
            PromptTemplate.Create(Guid.NewGuid(), UserA, "Mine", "x", "y"), TestContext.Current.CancellationToken);
        PromptTemplate system = await _sut.CreateAsync(
            PromptTemplate.CreateSystem(Guid.NewGuid(), "Prompt:Draft:Name", "x", "z"), TestContext.Current.CancellationToken);

        (await _sut.DeleteAsync(system.Id, UserA, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await _sut.DeleteAsync(mine.Id, UserB, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await _sut.DeleteAsync(mine.Id, UserA, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await _sut.GetAsync(mine.Id, UserA, TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
