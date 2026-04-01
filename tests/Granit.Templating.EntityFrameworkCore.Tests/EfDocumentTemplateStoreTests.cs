using Granit.Exceptions;
using Granit.Guids;
using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.EntityFrameworkCore.Internal;
using Granit.Templating.Exceptions;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Store;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.EntityFrameworkCore.Tests;

public sealed class EfDocumentTemplateStoreTests
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

    private static HybridCache CreateHybridCache()
    {
        ServiceCollection services = new();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static ITemplateTransitionHook CreateAllowAllHook()
    {
        ITemplateTransitionHook hook = Substitute.For<ITemplateTransitionHook>();
        hook.CanTransitionAsync(Arg.Any<TemplateLifecycleStatus>(), Arg.Any<TemplateLifecycleStatus>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return hook;
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

    private static EfDocumentTemplateStore CreateStore(string dbName) =>
        new(new InMemoryContextFactory(dbName), CreateHybridCache(), CreateGuidGenerator(), CreateClock(), CreateAllowAllHook());

    private static EfDocumentTemplateStore CreateStore(string dbName, ITemplateTransitionHook hook) =>
        new(new InMemoryContextFactory(dbName), CreateHybridCache(), CreateGuidGenerator(), CreateClock(), hook);

    private static string NewDb() => Guid.NewGuid().ToString();

    // -------------------------------------------------------------------------
    // TryGetPublishedAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryGetPublishedAsync_WhenNothingPublished_ReturnsNull()
    {
        EfDocumentTemplateStore store = CreateStore(NewDb());
        TemplateDescriptor? result = await store.TryGetPublishedAsync(
            new TemplateKey("Billing.Invoice"),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryGetPublishedAsync_AfterPublish_ReturnsDescriptor()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Billing.Invoice");

        await store.SaveDraftAsync(key, "<p>Hello</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            cancellationToken: TestContext.Current.CancellationToken);

        TemplateDescriptor? result = await store.TryGetPublishedAsync(key,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Content.ShouldBe("<p>Hello</p>");
        result.MimeType.ShouldBe("text/html");
        result.RevisionId.ShouldNotBeNull();
        result.RevisionId!.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task TryGetPublishedAsync_AfterUnpublish_ReturnsNull()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Billing.Invoice");

        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.UnpublishAsync(key, "carol",
            cancellationToken: TestContext.Current.CancellationToken);

        TemplateDescriptor? result = await store.TryGetPublishedAsync(key,
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryGetPublishedAsync_WithCulture_ReturnsCultureSpecificTemplate()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey frKey = new("Billing.Invoice", "fr-BE");
        TemplateKey neutralKey = new("Billing.Invoice");

        await store.SaveDraftAsync(frKey, "<p>Français</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(frKey, "bob",
            cancellationToken: TestContext.Current.CancellationToken);

        TemplateDescriptor? frResult = await store.TryGetPublishedAsync(frKey,
            TestContext.Current.CancellationToken);
        TemplateDescriptor? neutralResult = await store.TryGetPublishedAsync(neutralKey,
            TestContext.Current.CancellationToken);

        frResult.ShouldNotBeNull();
        frResult!.Content.ShouldBe("<p>Français</p>");
        neutralResult.ShouldBeNull("culture-neutral key is distinct from fr-BE");
    }

    [Fact]
    public async Task TryGetPublishedAsync_AfterPublish_CacheIsInvalidated()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Cache.Publish");

        // Publish v1 and read (populates cache)
        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            cancellationToken: TestContext.Current.CancellationToken);
        TemplateDescriptor? v1 = await store.TryGetPublishedAsync(key,
            TestContext.Current.CancellationToken);
        v1!.Content.ShouldBe("<p>v1</p>");

        // Publish v2 — must invalidate the cache entry for v1
        await store.SaveDraftAsync(key, "<p>v2</p>", "text/html", "carol",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "dave",
            TestContext.Current.CancellationToken);

        TemplateDescriptor? v2 = await store.TryGetPublishedAsync(key,
            TestContext.Current.CancellationToken);

        v2!.Content.ShouldBe("<p>v2</p>", "cache must be invalidated after publish");
    }

    [Fact]
    public async Task TryGetPublishedAsync_AfterUnpublish_CacheIsInvalidated()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Cache.Unpublish");

        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            cancellationToken: TestContext.Current.CancellationToken);

        // Read to populate cache
        TemplateDescriptor? cached = await store.TryGetPublishedAsync(key,
            TestContext.Current.CancellationToken);
        cached.ShouldNotBeNull();

        // Unpublish — must invalidate the cache entry
        await store.UnpublishAsync(key, "carol",
            cancellationToken: TestContext.Current.CancellationToken);

        TemplateDescriptor? result = await store.TryGetPublishedAsync(key,
            TestContext.Current.CancellationToken);

        result.ShouldBeNull("cache must be invalidated after unpublish");
    }

    // -------------------------------------------------------------------------
    // SaveDraftAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveDraftAsync_CalledTwice_UpdatesExistingDraft()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Notifications.Welcome");

        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.SaveDraftAsync(key, "<p>v2</p>", "text/html", "bob",
            cancellationToken: TestContext.Current.CancellationToken);

        // Verify only one draft exists (no duplicate rows)
        await using TemplatingDbContext ctx = new InMemoryContextFactory(db).CreateDbContext();
        int count = await ctx.TemplateRevisions.CountAsync(
            r => r.TemplateName == key.Name && r.Status == TemplateLifecycleStatus.Draft,
            TestContext.Current.CancellationToken);

        count.ShouldBe(1, "SaveDraftAsync must upsert, not append");

        // Verify the content was updated
        await store.PublishAsync(key, "carol", TestContext.Current.CancellationToken);
        TemplateDescriptor? published = await store.TryGetPublishedAsync(key,
            TestContext.Current.CancellationToken);
        published!.Content.ShouldBe("<p>v2</p>", "latest draft content must be published");
    }

    // -------------------------------------------------------------------------
    // PublishAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_ArchivesPreviousPublishedRevision()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Billing.Invoice");

        // First publication
        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            cancellationToken: TestContext.Current.CancellationToken);

        // Second publication
        await store.SaveDraftAsync(key, "<p>v2</p>", "text/html", "carol",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "dave",
            TestContext.Current.CancellationToken);

        await using TemplatingDbContext ctx = new InMemoryContextFactory(db).CreateDbContext();
        int archivedCount = await ctx.TemplateRevisions.CountAsync(
            r => r.TemplateName == key.Name && r.Status == TemplateLifecycleStatus.Archived,
            TestContext.Current.CancellationToken);
        int publishedCount = await ctx.TemplateRevisions.CountAsync(
            r => r.TemplateName == key.Name && r.Status == TemplateLifecycleStatus.Published,
            TestContext.Current.CancellationToken);

        archivedCount.ShouldBe(1, "v1 must be archived");
        publishedCount.ShouldBe(1, "only v2 must be published");
    }

    [Fact]
    public async Task PublishAsync_NoDraftExists_ThrowsNotFoundException()
    {
        EfDocumentTemplateStore store = CreateStore(NewDb());
        TemplateKey key = new("Ghost.Template");

        Func<Task> act = () => store.PublishAsync(key, "alice",
            cancellationToken: TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<NotFoundException>(act)).Message.ShouldContain("no draft");
    }

    [Fact]
    public async Task PublishAsync_HookDeniesTransition_ThrowsTemplateTransitionDeniedException()
    {
        string db = NewDb();
        ITemplateTransitionHook hook = Substitute.For<ITemplateTransitionHook>();
        hook.CanTransitionAsync(TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published, Arg.Any<CancellationToken>())
            .Returns(false);

        EfDocumentTemplateStore store = CreateStore(db, hook);
        TemplateKey key = new("Billing.Invoice");

        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);

        Func<Task> act = () => store.PublishAsync(key, "bob",
            cancellationToken: TestContext.Current.CancellationToken);

        TemplateTransitionDeniedException ex = await Should.ThrowAsync<TemplateTransitionDeniedException>(act);
        ex.From.ShouldBe(TemplateLifecycleStatus.Draft);
        ex.To.ShouldBe(TemplateLifecycleStatus.Published);
    }

    [Fact]
    public async Task PublishAsync_HookOnTransitionedAsync_CalledAfterPersist()
    {
        string db = NewDb();
        ITemplateTransitionHook hook = Substitute.For<ITemplateTransitionHook>();
        hook.CanTransitionAsync(Arg.Any<TemplateLifecycleStatus>(), Arg.Any<TemplateLifecycleStatus>(), Arg.Any<CancellationToken>())
            .Returns(true);

        EfDocumentTemplateStore store = CreateStore(db, hook);
        TemplateKey key = new("Billing.Invoice");

        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            cancellationToken: TestContext.Current.CancellationToken);

        await hook.Received(1).OnTransitionedAsync(
            Arg.Any<Guid>(),
            TemplateLifecycleStatus.Draft,
            TemplateLifecycleStatus.Published,
            "bob",
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // UnpublishAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UnpublishAsync_WhenNothingPublished_DoesNotThrow()
    {
        EfDocumentTemplateStore store = CreateStore(NewDb());
        TemplateKey key = new("Ghost.Template");

        Func<Task> act = () => store.UnpublishAsync(key, "alice",
            cancellationToken: TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task UnpublishAsync_HookDeniesTransition_ThrowsTemplateTransitionDeniedException()
    {
        string db = NewDb();
        ITemplateTransitionHook hook = Substitute.For<ITemplateTransitionHook>();
        hook.CanTransitionAsync(TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published, Arg.Any<CancellationToken>())
            .Returns(true);
        hook.CanTransitionAsync(TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Archived, Arg.Any<CancellationToken>())
            .Returns(false);

        EfDocumentTemplateStore store = CreateStore(db, hook);
        TemplateKey key = new("Billing.Invoice");

        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            cancellationToken: TestContext.Current.CancellationToken);

        Func<Task> act = () => store.UnpublishAsync(key, "carol",
            cancellationToken: TestContext.Current.CancellationToken);

        TemplateTransitionDeniedException ex = await Should.ThrowAsync<TemplateTransitionDeniedException>(act);
        ex.From.ShouldBe(TemplateLifecycleStatus.Published);
        ex.To.ShouldBe(TemplateLifecycleStatus.Archived);
    }

    // -------------------------------------------------------------------------
    // DeleteDraftAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteDraftAsync_RemovesDraftRow()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Billing.Invoice");

        await store.SaveDraftAsync(key, "<p>draft</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.DeleteDraftAsync(key, "alice",
            cancellationToken: TestContext.Current.CancellationToken);

        await using TemplatingDbContext ctx = new InMemoryContextFactory(db).CreateDbContext();
        int count = await ctx.TemplateRevisions.CountAsync(
            r => r.TemplateName == key.Name,
            TestContext.Current.CancellationToken);

        count.ShouldBe(0, "draft must be physically deleted");
    }

    [Fact]
    public async Task DeleteDraftAsync_NoDraftExists_ThrowsNotFoundException()
    {
        EfDocumentTemplateStore store = CreateStore(NewDb());
        TemplateKey key = new("Ghost.Template");

        Func<Task> act = () => store.DeleteDraftAsync(key, "alice",
            cancellationToken: TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<NotFoundException>(act)).Message.ShouldContain("no draft");
    }

    [Fact]
    public async Task DeleteDraftAsync_ArchivedRowsArePreserved()
    {
        // Ensures published/archived revisions survive even after a draft is deleted
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Billing.Invoice");

        // Publish v1, then create a new draft and delete it
        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.SaveDraftAsync(key, "<p>v2-draft</p>", "text/html", "carol",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.UnpublishAsync(key, "dave",
            TestContext.Current.CancellationToken);
        await store.DeleteDraftAsync(key, "carol",
            cancellationToken: TestContext.Current.CancellationToken);

        await using TemplatingDbContext ctx = new InMemoryContextFactory(db).CreateDbContext();
        int archivedCount = await ctx.TemplateRevisions.CountAsync(
            r => r.TemplateName == key.Name && r.Status == TemplateLifecycleStatus.Archived,
            TestContext.Current.CancellationToken);

        archivedCount.ShouldBe(1, "archived revision must be preserved for ISO 27001 audit trail");
    }

    // -------------------------------------------------------------------------
    // GetHistoryAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetHistoryAsync_ReturnsAllRevisionsNewestFirst()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Billing.Invoice");

        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.SaveDraftAsync(key, "<p>v2</p>", "text/html", "carol",
            cancellationToken: TestContext.Current.CancellationToken);

        IReadOnlyList<TemplateRevision> history = await store.GetHistoryAsync(key,
            TestContext.Current.CancellationToken);

        history.Count.ShouldBe(2);
        // Draft (v2) was created last → appears first
        history[0].Status.ShouldBe(TemplateLifecycleStatus.Draft);
        history[1].Status.ShouldBe(TemplateLifecycleStatus.Published);
    }
}
