using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.EntityFrameworkCore.Internal;
using Granit.Templating.Keys;
using Granit.Templating.Store;
using Granit.Timing;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.EntityFrameworkCore.Tests;

/// <summary>
/// Additional tests for <see cref="EfDocumentTemplateStore"/> covering
/// TryGetDraftAsync and edge cases, plus projection tests for
/// <see cref="EfTemplateSummaryQueryableSource"/> (powers the
/// <c>GET /templates</c> Query Engine endpoint).
/// </summary>
public sealed class EfDocumentTemplateStoreAdditionalTests
{
    // -------------------------------------------------------------------------
    // Test infrastructure (shared with EfDocumentTemplateStoreTests)
    // -------------------------------------------------------------------------

    private sealed class InMemoryContextFactory(string dbName)
        : IDbContextFactory<TemplatingDbContext>
    {
        public TemplatingDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<TemplatingDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options,
                GranitDesignTime.CurrentTenant);

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
        hook.CanTransitionAsync(Arg.Any<WorkflowLifecycleStatus>(), Arg.Any<WorkflowLifecycleStatus>(), Arg.Any<CancellationToken>())
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

    private static string NewDb() => Guid.NewGuid().ToString();

    // -------------------------------------------------------------------------
    // TryGetDraftAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryGetDraftAsync_WhenDraftExists_ReturnsRevision()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Notifications.Welcome");

        await store.SaveDraftAsync(key, "<p>Draft content</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);

        TemplateRevision? result = await store.TryGetDraftAsync(key,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Content.ShouldBe("<p>Draft content</p>");
        result.MimeType.ShouldBe("text/html");
        result.Status.ShouldBe(WorkflowLifecycleStatus.Draft);
        result.CreatedBy.ShouldBe("alice");
    }

    [Fact]
    public async Task TryGetDraftAsync_WhenNoDraft_ReturnsNull()
    {
        EfDocumentTemplateStore store = CreateStore(NewDb());
        TemplateKey key = new("Ghost.Template");

        TemplateRevision? result = await store.TryGetDraftAsync(key,
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryGetDraftAsync_OnlyPublished_ReturnsNull()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Billing.Invoice");

        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            TestContext.Current.CancellationToken);

        TemplateRevision? result = await store.TryGetDraftAsync(key,
            TestContext.Current.CancellationToken);

        result.ShouldBeNull("no draft should exist after publishing");
    }

    // -------------------------------------------------------------------------
    // EfTemplateSummaryQueryableSource — projection over TemplateRevisionEntity
    // -------------------------------------------------------------------------

    private static EfTemplateSummaryQueryableSource CreateQueryableSource(string dbName) =>
        new(new InMemoryContextFactory(dbName), GranitDesignTime.CurrentTenant);

    [Fact]
    public void TemplateSummary_EmptyStore_ReturnsEmptyQueryable()
    {
        EfTemplateSummaryQueryableSource source = CreateQueryableSource(NewDb());

        List<TemplateSummary> summaries = [.. source.GetQueryable()];

        summaries.ShouldBeEmpty();
    }

    [Fact]
    public async Task TemplateSummary_ExcludesArchivedRevisions()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);
        TemplateKey key = new("Billing.Invoice");

        // Create draft, publish, then create a new draft and publish (archiving v1).
        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            TestContext.Current.CancellationToken);
        await store.SaveDraftAsync(key, "<p>v2</p>", "text/html", "carol",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "dave",
            TestContext.Current.CancellationToken);

        List<TemplateSummary> summaries = [.. CreateQueryableSource(db).GetQueryable()];

        // Only one logical template row — published v2 (archived v1 excluded).
        summaries.Count.ShouldBe(1);
        summaries[0].Name.ShouldBe("Billing.Invoice");
        summaries[0].HasPublishedVersion.ShouldBeTrue();
    }

    [Fact]
    public async Task TemplateSummary_DistinctTemplates_ProduceOneRowEach()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);

        await store.SaveDraftAsync(new TemplateKey("Billing.Invoice"), "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.SaveDraftAsync(new TemplateKey("Notifications.Welcome"), "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);

        List<TemplateSummary> summaries = [.. CreateQueryableSource(db).GetQueryable()];

        summaries.Count.ShouldBe(2);
        summaries.Select(s => s.Name).ShouldBe(["Billing.Invoice", "Notifications.Welcome"], ignoreOrder: true);
    }

    [Fact]
    public async Task TemplateSummary_DraftAndPublished_ResolveToDraftStatus()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);

        await store.SaveDraftAsync(new TemplateKey("Draft.Only"), "<p>draft</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.SaveDraftAsync(new TemplateKey("Published.One"), "<p>published</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(new TemplateKey("Published.One"), "bob",
            TestContext.Current.CancellationToken);

        List<TemplateSummary> summaries = [.. CreateQueryableSource(db).GetQueryable()];

        TemplateSummary draft = summaries.Single(s => s.Name == "Draft.Only");
        draft.CurrentStatus.ShouldBe(WorkflowLifecycleStatus.Draft);
        draft.HasPublishedVersion.ShouldBeFalse();

        TemplateSummary published = summaries.Single(s => s.Name == "Published.One");
        published.CurrentStatus.ShouldBe(WorkflowLifecycleStatus.Published);
        published.HasPublishedVersion.ShouldBeTrue();
    }

    [Fact]
    public async Task TemplateSummary_CulturesProduceSeparateRows()
    {
        string db = NewDb();
        EfDocumentTemplateStore store = CreateStore(db);

        await store.SaveDraftAsync(new TemplateKey("Invoice", "fr"), "<p>FR</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.SaveDraftAsync(new TemplateKey("Invoice", "en"), "<p>EN</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);

        List<TemplateSummary> summaries = [.. CreateQueryableSource(db).GetQueryable()];

        summaries.Count.ShouldBe(2);
        summaries.Select(s => s.Culture).ShouldBe(["fr", "en"], ignoreOrder: true);
    }

    [Fact]
    public async Task TemplateSummary_CategoryIdSurfacedFromLatestRevision()
    {
        string db = NewDb();
        var categoryId = Guid.NewGuid();

        await using TemplatingDbContext ctx = new InMemoryContextFactory(db).CreateDbContext();
        ctx.TemplateRevisions.AddRange(
            TemplateRevisionEntity.Create(
                Guid.NewGuid(),
                templateName: "WithCategory",
                culture: null,
                content: "<p>A</p>",
                mimeType: "text/html",
                categoryId: categoryId),
            TemplateRevisionEntity.Create(
                Guid.NewGuid(),
                templateName: "WithoutCategory",
                culture: null,
                content: "<p>B</p>",
                mimeType: "text/html"));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<TemplateSummary> summaries = [.. CreateQueryableSource(db).GetQueryable()];

        summaries.Count.ShouldBe(2);
        summaries.Single(s => s.Name == "WithCategory").CategoryId.ShouldBe(categoryId);
        summaries.Single(s => s.Name == "WithoutCategory").CategoryId.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // UnpublishAsync — hook callback
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UnpublishAsync_CallsOnTransitionedForEachPublishedRevision()
    {
        string db = NewDb();
        ITemplateTransitionHook hook = Substitute.For<ITemplateTransitionHook>();
        hook.CanTransitionAsync(Arg.Any<WorkflowLifecycleStatus>(), Arg.Any<WorkflowLifecycleStatus>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var store = new EfDocumentTemplateStore(
            new InMemoryContextFactory(db), CreateHybridCache(), CreateGuidGenerator(), CreateClock(), hook);

        TemplateKey key = new("Billing.Invoice");
        await store.SaveDraftAsync(key, "<p>v1</p>", "text/html", "alice",
            cancellationToken: TestContext.Current.CancellationToken);
        await store.PublishAsync(key, "bob",
            TestContext.Current.CancellationToken);

        // Reset received calls to focus on UnpublishAsync.
        hook.ClearReceivedCalls();

        await store.UnpublishAsync(key, "carol",
            cancellationToken: TestContext.Current.CancellationToken);

        await hook.Received(1).OnTransitionedAsync(
            Arg.Any<Guid>(),
            WorkflowLifecycleStatus.Published,
            WorkflowLifecycleStatus.Archived,
            "carol",
            Arg.Any<CancellationToken>());
    }
}
