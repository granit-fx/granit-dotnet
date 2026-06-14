using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.EntityFrameworkCore.Internal;
using Granit.Guids;
using Shouldly;

namespace Granit.AI.Prompts.EntityFrameworkCore.Tests;

public sealed class EfPromptTemplateDataManagerTests : IDisposable
{
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfPromptTemplateStore _store;
    private readonly EfPromptTemplateDataManager _sut;

    public EfPromptTemplateDataManagerTests()
    {
        _store = new EfPromptTemplateStore(_factory, new SimpleGuidGenerator());
        _sut = new EfPromptTemplateDataManager(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetAllForOwner_returns_the_owners_prompts_excluding_system_and_other_users()
    {
        await _store.CreateAsync(PromptTemplate.Create(Guid.NewGuid(), UserA, "Mine", "x", "y"), TestContext.Current.CancellationToken);
        await _store.CreateAsync(PromptTemplate.Create(Guid.NewGuid(), UserB, "Theirs", "x", "y"), TestContext.Current.CancellationToken);
        await _store.CreateAsync(PromptTemplate.CreateSystem(Guid.NewGuid(), "Seed", "x", "y"), TestContext.Current.CancellationToken);

        IReadOnlyList<PromptTemplate> owned = await _sut.GetAllForOwnerAsync(UserA, TestContext.Current.CancellationToken);

        owned.Count.ShouldBe(1);
        owned[0].Name.ShouldBe("Mine");
    }

    [Fact]
    public async Task EraseOwner_hard_deletes_the_owners_prompts_and_their_category_links()
    {
        var prompt = PromptTemplate.Create(Guid.NewGuid(), UserA, "Mine", "x", "y");
        prompt.AssignCategory(Guid.NewGuid(), Guid.NewGuid());
        await _store.CreateAsync(prompt, TestContext.Current.CancellationToken);

        int erased = await _sut.EraseOwnerAsync(tenantId: null, UserA, TestContext.Current.CancellationToken);

        erased.ShouldBe(1);
        (await _sut.GetAllForOwnerAsync(UserA, TestContext.Current.CancellationToken)).ShouldBeEmpty();

        await using AIPromptsDbContext context = _factory.CreateDbContext();
        context.Set<PromptTemplateCategory>().Count().ShouldBe(0);
    }

    [Fact]
    public async Task EraseOwner_never_touches_system_prompts_or_other_users()
    {
        await _store.CreateAsync(PromptTemplate.Create(Guid.NewGuid(), UserA, "Mine", "x", "y"), TestContext.Current.CancellationToken);
        await _store.CreateAsync(PromptTemplate.Create(Guid.NewGuid(), UserB, "Theirs", "x", "y"), TestContext.Current.CancellationToken);
        PromptTemplate system = await _store.CreateAsync(
            PromptTemplate.CreateSystem(Guid.NewGuid(), "Seed", "x", "y"), TestContext.Current.CancellationToken);

        await _sut.EraseOwnerAsync(tenantId: null, UserA, TestContext.Current.CancellationToken);

        (await _store.GetAsync(system.Id, UserB, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        (await _sut.GetAllForOwnerAsync(UserB, TestContext.Current.CancellationToken)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task EraseOwner_is_idempotent()
    {
        await _store.CreateAsync(PromptTemplate.Create(Guid.NewGuid(), UserA, "Mine", "x", "y"), TestContext.Current.CancellationToken);

        (await _sut.EraseOwnerAsync(tenantId: null, UserA, TestContext.Current.CancellationToken)).ShouldBe(1);
        (await _sut.EraseOwnerAsync(tenantId: null, UserA, TestContext.Current.CancellationToken)).ShouldBe(0);
    }
}
