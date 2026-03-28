using Granit.Exceptions;
using Granit.Guids;
using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.EntityFrameworkCore.Internal;
using Granit.Templating.Store;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.EntityFrameworkCore.Tests;

public sealed class EfTemplateCategoryStoreTests
{
    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private sealed class InMemoryContextFactory(string dbName)
        : IDbContextFactory<TemplatingDbContext>
    {
        public TemplatingDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<TemplatingDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options);

        public Task<TemplatingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private static IGuidGenerator CreateGuidGenerator()
    {
        IGuidGenerator gen = Substitute.For<IGuidGenerator>();
        gen.Create().Returns(_ => Guid.NewGuid());
        return gen;
    }

    private static IClock CreateClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(_ => DateTimeOffset.UtcNow);
        return clock;
    }

    private static EfTemplateCategoryStore CreateStore(string dbName) =>
        new(new InMemoryContextFactory(dbName), CreateGuidGenerator(), CreateClock());

    private static string NewDb() => Guid.NewGuid().ToString();

    // -------------------------------------------------------------------------
    // ListCategoriesAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListCategoriesAsync_EmptyStore_ReturnsEmptyList()
    {
        EfTemplateCategoryStore store = CreateStore(NewDb());

        IReadOnlyList<TemplateCategory> result = await store.ListCategoriesAsync(
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListCategoriesAsync_ReturnsCategoriesOrderedBySortOrderThenName()
    {
        string db = NewDb();
        EfTemplateCategoryStore store = CreateStore(db);

        await store.CreateCategoryAsync("Zebra", null, null, 2, "alice",
            TestContext.Current.CancellationToken);
        await store.CreateCategoryAsync("Alpha", null, null, 1, "alice",
            TestContext.Current.CancellationToken);
        await store.CreateCategoryAsync("Beta", null, null, 1, "alice",
            TestContext.Current.CancellationToken);

        IReadOnlyList<TemplateCategory> result = await store.ListCategoriesAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);
        result[0].Name.ShouldBe("Alpha");
        result[1].Name.ShouldBe("Beta");
        result[2].Name.ShouldBe("Zebra");
    }

    [Fact]
    public async Task ListCategoriesAsync_IncludesTemplateCount()
    {
        string db = NewDb();
        EfTemplateCategoryStore store = CreateStore(db);

        TemplateCategory category = await store.CreateCategoryAsync(
            "Invoices", null, null, 0, "alice",
            TestContext.Current.CancellationToken);

        // Seed a template revision linked to this category.
        await using TemplatingDbContext ctx = new InMemoryContextFactory(db).CreateDbContext();
        ctx.TemplateRevisions.Add(new TemplateRevisionEntity
        {
            RevisionId = Guid.NewGuid(),
            TemplateName = "Invoice.Main",
            Culture = null,
            Content = "<p>Invoice</p>",
            MimeType = "text/html",
            Status = TemplateLifecycleStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "alice",
            CategoryId = category.Id,
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<TemplateCategory> result = await store.ListCategoriesAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].TemplateCount.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // GetCategoryAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetCategoryAsync_ExistingId_ReturnsCategory()
    {
        string db = NewDb();
        EfTemplateCategoryStore store = CreateStore(db);

        TemplateCategory created = await store.CreateCategoryAsync(
            "Letters", "Patient letters", "mail", 1, "alice",
            TestContext.Current.CancellationToken);

        TemplateCategory? result = await store.GetCategoryAsync(created.Id,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(created.Id);
        result.Name.ShouldBe("Letters");
        result.Description.ShouldBe("Patient letters");
        result.Icon.ShouldBe("mail");
        result.SortOrder.ShouldBe(1);
    }

    [Fact]
    public async Task GetCategoryAsync_NonExistentId_ReturnsNull()
    {
        EfTemplateCategoryStore store = CreateStore(NewDb());

        TemplateCategory? result = await store.GetCategoryAsync(Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCategoryAsync_IncludesTemplateCount()
    {
        string db = NewDb();
        EfTemplateCategoryStore store = CreateStore(db);

        TemplateCategory category = await store.CreateCategoryAsync(
            "Reports", null, null, 0, "alice",
            TestContext.Current.CancellationToken);

        // Seed two template revisions (different names) linked to this category.
        await using TemplatingDbContext ctx = new InMemoryContextFactory(db).CreateDbContext();
        ctx.TemplateRevisions.AddRange(
            new TemplateRevisionEntity
            {
                RevisionId = Guid.NewGuid(),
                TemplateName = "Report.A",
                Content = "<p>A</p>",
                MimeType = "text/html",
                Status = TemplateLifecycleStatus.Published,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "alice",
                CategoryId = category.Id,
            },
            new TemplateRevisionEntity
            {
                RevisionId = Guid.NewGuid(),
                TemplateName = "Report.B",
                Content = "<p>B</p>",
                MimeType = "text/html",
                Status = TemplateLifecycleStatus.Draft,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "bob",
                CategoryId = category.Id,
            });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        TemplateCategory? result = await store.GetCategoryAsync(category.Id,
            TestContext.Current.CancellationToken);

        result!.TemplateCount.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // CreateCategoryAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateCategoryAsync_ReturnsNewCategory()
    {
        EfTemplateCategoryStore store = CreateStore(NewDb());

        TemplateCategory result = await store.CreateCategoryAsync(
            "Notifications", "Email templates", "bell", 5, "alice",
            TestContext.Current.CancellationToken);

        result.Id.ShouldNotBe(Guid.Empty);
        result.Name.ShouldBe("Notifications");
        result.Description.ShouldBe("Email templates");
        result.Icon.ShouldBe("bell");
        result.SortOrder.ShouldBe(5);
        result.TemplateCount.ShouldBe(0);
    }

    [Fact]
    public async Task CreateCategoryAsync_DuplicateName_ThrowsConflictException()
    {
        string db = NewDb();
        EfTemplateCategoryStore store = CreateStore(db);

        await store.CreateCategoryAsync("Letters", null, null, 0, "alice",
            TestContext.Current.CancellationToken);

        Func<Task> act = () => store.CreateCategoryAsync("Letters", null, null, 0, "bob",
            TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<ConflictException>(act)).Message.ShouldContain("Letters");
    }

    // -------------------------------------------------------------------------
    // UpdateCategoryAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateCategoryAsync_UpdatesAllFields()
    {
        string db = NewDb();
        EfTemplateCategoryStore store = CreateStore(db);

        TemplateCategory created = await store.CreateCategoryAsync(
            "Old Name", "Old desc", "old-icon", 1, "alice",
            TestContext.Current.CancellationToken);

        TemplateCategory updated = await store.UpdateCategoryAsync(
            created.Id, "New Name", "New desc", "new-icon", 10,
            TestContext.Current.CancellationToken);

        updated.Id.ShouldBe(created.Id);
        updated.Name.ShouldBe("New Name");
        updated.Description.ShouldBe("New desc");
        updated.Icon.ShouldBe("new-icon");
        updated.SortOrder.ShouldBe(10);
    }

    [Fact]
    public async Task UpdateCategoryAsync_NonExistentId_ThrowsEntityNotFoundException()
    {
        EfTemplateCategoryStore store = CreateStore(NewDb());

        Func<Task> act = () => store.UpdateCategoryAsync(
            Guid.NewGuid(), "Name", null, null, 0,
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<EntityNotFoundException>(act);
    }

    [Fact]
    public async Task UpdateCategoryAsync_DuplicateName_ThrowsConflictException()
    {
        string db = NewDb();
        EfTemplateCategoryStore store = CreateStore(db);

        await store.CreateCategoryAsync("Alpha", null, null, 0, "alice",
            TestContext.Current.CancellationToken);
        TemplateCategory beta = await store.CreateCategoryAsync("Beta", null, null, 0, "alice",
            TestContext.Current.CancellationToken);

        Func<Task> act = () => store.UpdateCategoryAsync(
            beta.Id, "Alpha", null, null, 0,
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ConflictException>(act);
    }

    [Fact]
    public async Task UpdateCategoryAsync_SameNameSameEntity_DoesNotThrow()
    {
        string db = NewDb();
        EfTemplateCategoryStore store = CreateStore(db);

        TemplateCategory created = await store.CreateCategoryAsync(
            "Letters", null, null, 0, "alice",
            TestContext.Current.CancellationToken);

        TemplateCategory updated = await store.UpdateCategoryAsync(
            created.Id, "Letters", "Updated description", null, 5,
            TestContext.Current.CancellationToken);

        updated.Description.ShouldBe("Updated description");
    }

    // -------------------------------------------------------------------------
    // DeleteCategoryAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteCategoryAsync_RemovesCategory()
    {
        string db = NewDb();
        EfTemplateCategoryStore store = CreateStore(db);

        TemplateCategory created = await store.CreateCategoryAsync(
            "ToDelete", null, null, 0, "alice",
            TestContext.Current.CancellationToken);

        await store.DeleteCategoryAsync(created.Id,
            TestContext.Current.CancellationToken);

        TemplateCategory? result = await store.GetCategoryAsync(created.Id,
            TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteCategoryAsync_NonExistentId_ThrowsEntityNotFoundException()
    {
        EfTemplateCategoryStore store = CreateStore(NewDb());

        Func<Task> act = () => store.DeleteCategoryAsync(Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<EntityNotFoundException>(act);
    }

    [Fact]
    public async Task DeleteCategoryAsync_WithAssociatedTemplates_ThrowsConflictException()
    {
        string db = NewDb();
        EfTemplateCategoryStore store = CreateStore(db);

        TemplateCategory category = await store.CreateCategoryAsync(
            "InUse", null, null, 0, "alice",
            TestContext.Current.CancellationToken);

        // Seed a template revision linked to this category.
        await using TemplatingDbContext ctx = new InMemoryContextFactory(db).CreateDbContext();
        ctx.TemplateRevisions.Add(new TemplateRevisionEntity
        {
            RevisionId = Guid.NewGuid(),
            TemplateName = "Invoice.Main",
            Content = "<p>Invoice</p>",
            MimeType = "text/html",
            Status = TemplateLifecycleStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "alice",
            CategoryId = category.Id,
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        Func<Task> act = () => store.DeleteCategoryAsync(category.Id,
            TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<ConflictException>(act)).Message.ShouldContain("1");
    }
}
