using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.EntityFrameworkCore.Tests.Internal;

public sealed class CategoryServiceSqliteTests : IAsyncLifetime
{
    private SqliteConnection _holdOpen = null!;
    private TestDbContextFactory _factory = null!;
    private CategoryService _sut = null!;
    private CategoryAssignmentService _assignmentSut = null!;
    private CapturingLocalEventBus _localBus = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private const string DocumentType = "Granit.Documents.Domain.Document";

    public async ValueTask InitializeAsync()
    {
        string connectionString =
            $"DataSource=file:taxonomy-cat-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _holdOpen = new SqliteConnection(connectionString);
        await _holdOpen.OpenAsync();

        DbContextOptions<TaxonomyDbContext> options = new DbContextOptionsBuilder<TaxonomyDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using TaxonomyDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new TestDbContextFactory(options);

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(TenantId);

        ServiceCollection services = new();
        services.AddMetrics();
        TaxonomyMetrics metrics = new(
            services.BuildServiceProvider().GetRequiredService<IMeterFactory>());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        _localBus = new CapturingLocalEventBus();

        _sut = new CategoryService(_factory, currentTenant, new SimpleGuidGenerator(), _localBus, metrics);
        _assignmentSut = new CategoryAssignmentService(
            _factory, currentTenant, new SimpleGuidGenerator(), clock, _localBus, metrics);
    }

    public ValueTask DisposeAsync() => _holdOpen.DisposeAsync();

    // -------------------------------------------------------------------------
    // CategoryService — CRUD
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_RootAndChild_PersistsTree()
    {
        Category root = await _sut.CreateAsync("products", parentId: null, name: "electronics",
            cancellationToken: TestContext.Current.CancellationToken);
        Category child = await _sut.CreateAsync("products", parentId: root.Id, name: "laptops",
            cancellationToken: TestContext.Current.CancellationToken);

        root.Path.ShouldBe("/electronics");
        child.Path.ShouldBe("/electronics/laptops");
        child.Depth.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_ParentInDifferentScope_Throws()
    {
        Category root = await _sut.CreateAsync("products", parentId: null, name: "electronics",
            cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.CreateAsync("documents", parentId: root.Id, name: "child",
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RenameAsync_RootWithChildren_ReMaterialisesDescendants()
    {
        Category root = await _sut.CreateAsync("products", null, "electronics",
            cancellationToken: TestContext.Current.CancellationToken);
        Category child = await _sut.CreateAsync("products", root.Id, "laptops",
            cancellationToken: TestContext.Current.CancellationToken);
        Category grandchild = await _sut.CreateAsync("products", child.Id, "gaming",
            cancellationToken: TestContext.Current.CancellationToken);

        Category? renamed = await _sut.RenameAsync(root.Id, "hardware",
            cancellationToken: TestContext.Current.CancellationToken);

        renamed.ShouldNotBeNull();
        renamed.Path.ShouldBe("/hardware");

        Category? reloadedChild = await _sut.GetByIdAsync(child.Id, TestContext.Current.CancellationToken);
        reloadedChild!.Path.ShouldBe("/hardware/laptops");

        Category? reloadedGrandchild = await _sut.GetByIdAsync(grandchild.Id, TestContext.Current.CancellationToken);
        reloadedGrandchild!.Path.ShouldBe("/hardware/laptops/gaming");
    }

    [Fact]
    public async Task MoveAsync_DeepSubtree_ReMaterialisesDescendantPathsAndDepths()
    {
        // Build: /a/b/c/d   /a/b/e   and target /x
        Category a = await _sut.CreateAsync("products", null, "a",
            cancellationToken: TestContext.Current.CancellationToken);
        Category b = await _sut.CreateAsync("products", a.Id, "b",
            cancellationToken: TestContext.Current.CancellationToken);
        Category c = await _sut.CreateAsync("products", b.Id, "c",
            cancellationToken: TestContext.Current.CancellationToken);
        Category d = await _sut.CreateAsync("products", c.Id, "d",
            cancellationToken: TestContext.Current.CancellationToken);
        Category e = await _sut.CreateAsync("products", b.Id, "e",
            cancellationToken: TestContext.Current.CancellationToken);
        Category x = await _sut.CreateAsync("products", null, "x",
            cancellationToken: TestContext.Current.CancellationToken);

        // Move b under x
        Category? moved = await _sut.MoveAsync(b.Id, x.Id, TestContext.Current.CancellationToken);
        moved.ShouldNotBeNull();
        moved.Path.ShouldBe("/x/b");
        moved.Depth.ShouldBe(1);

        Category? reloadedC = await _sut.GetByIdAsync(c.Id, TestContext.Current.CancellationToken);
        reloadedC!.Path.ShouldBe("/x/b/c");
        reloadedC.Depth.ShouldBe(2);

        Category? reloadedD = await _sut.GetByIdAsync(d.Id, TestContext.Current.CancellationToken);
        reloadedD!.Path.ShouldBe("/x/b/c/d");
        reloadedD.Depth.ShouldBe(3);

        Category? reloadedE = await _sut.GetByIdAsync(e.Id, TestContext.Current.CancellationToken);
        reloadedE!.Path.ShouldBe("/x/b/e");
        reloadedE.Depth.ShouldBe(2);
    }

    [Fact]
    public async Task MoveAsync_PrefixCollision_DoesNotAffectSimilarlyNamedSiblings()
    {
        // Build: /a/inner   /ab/inner
        Category a = await _sut.CreateAsync("products", null, "a",
            cancellationToken: TestContext.Current.CancellationToken);
        Category ab = await _sut.CreateAsync("products", null, "ab",
            cancellationToken: TestContext.Current.CancellationToken);
        Category aInner = await _sut.CreateAsync("products", a.Id, "inner",
            cancellationToken: TestContext.Current.CancellationToken);
        Category abInner = await _sut.CreateAsync("products", ab.Id, "inner",
            cancellationToken: TestContext.Current.CancellationToken);
        Category x = await _sut.CreateAsync("products", null, "x",
            cancellationToken: TestContext.Current.CancellationToken);

        await _sut.MoveAsync(a.Id, x.Id, TestContext.Current.CancellationToken);

        Category? reloadedA = await _sut.GetByIdAsync(aInner.Id, TestContext.Current.CancellationToken);
        reloadedA!.Path.ShouldBe("/x/a/inner");
        Category? reloadedAb = await _sut.GetByIdAsync(abInner.Id, TestContext.Current.CancellationToken);
        reloadedAb!.Path.ShouldBe("/ab/inner"); // unchanged — '/' boundary prevents collision
    }

    [Fact]
    public async Task DeleteAsync_WithDescendants_Throws()
    {
        Category root = await _sut.CreateAsync("products", null, "electronics",
            cancellationToken: TestContext.Current.CancellationToken);
        await _sut.CreateAsync("products", root.Id, "laptops",
            cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.DeleteAsync(root.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_LeafCategory_RemovesRow()
    {
        Category root = await _sut.CreateAsync("products", null, "electronics",
            cancellationToken: TestContext.Current.CancellationToken);

        bool deleted = await _sut.DeleteAsync(root.Id, TestContext.Current.CancellationToken);
        deleted.ShouldBeTrue();

        Category? reloaded = await _sut.GetByIdAsync(root.Id, TestContext.Current.CancellationToken);
        reloaded.ShouldBeNull();
    }

    [Fact]
    public async Task ListByScopeAsync_OrdersByPath()
    {
        Category a = await _sut.CreateAsync("products", null, "a",
            cancellationToken: TestContext.Current.CancellationToken);
        await _sut.CreateAsync("products", a.Id, "child",
            cancellationToken: TestContext.Current.CancellationToken);
        await _sut.CreateAsync("products", null, "b",
            cancellationToken: TestContext.Current.CancellationToken);

        IReadOnlyList<Category> list = await _sut.ListByScopeAsync("products",
            TestContext.Current.CancellationToken);

        list.Count.ShouldBe(3);
        list.Select(c => c.Path).ShouldBe(["/a", "/a/child", "/b"]);
    }

    // -------------------------------------------------------------------------
    // CategoryAssignmentService — single-assignment semantics
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Assign_FirstCall_CreatesRow()
    {
        Category cat = await _sut.CreateAsync("products", null, "electronics",
            cancellationToken: TestContext.Current.CancellationToken);
        var targetId = Guid.NewGuid();

        CategoryAssignment assignment = await _assignmentSut.AssignAsync(
            cat.Id, DocumentType, targetId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        assignment.CategoryId.ShouldBe(cat.Id);
    }

    [Fact]
    public async Task Assign_SameTarget_DifferentCategory_ReplacesAssignment()
    {
        Category catA = await _sut.CreateAsync("products", null, "a",
            cancellationToken: TestContext.Current.CancellationToken);
        Category catB = await _sut.CreateAsync("products", null, "b",
            cancellationToken: TestContext.Current.CancellationToken);
        var targetId = Guid.NewGuid();

        CategoryAssignment first = await _assignmentSut.AssignAsync(
            catA.Id, DocumentType, targetId, Guid.NewGuid(), TestContext.Current.CancellationToken);
        CategoryAssignment second = await _assignmentSut.AssignAsync(
            catB.Id, DocumentType, targetId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        first.Id.ShouldBe(second.Id); // same row, updated in place
        second.CategoryId.ShouldBe(catB.Id);
    }

    [Fact]
    public async Task GetForTarget_ReturnsAssignedCategory()
    {
        Category cat = await _sut.CreateAsync("products", null, "electronics",
            cancellationToken: TestContext.Current.CancellationToken);
        var targetId = Guid.NewGuid();
        await _assignmentSut.AssignAsync(cat.Id, DocumentType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Category? got = await _assignmentSut.GetForTargetAsync(DocumentType, targetId,
            TestContext.Current.CancellationToken);

        got.ShouldNotBeNull();
        got.Id.ShouldBe(cat.Id);
    }

    [Fact]
    public async Task UnassignAsync_RemovesRow()
    {
        Category cat = await _sut.CreateAsync("products", null, "electronics",
            cancellationToken: TestContext.Current.CancellationToken);
        var targetId = Guid.NewGuid();
        await _assignmentSut.AssignAsync(cat.Id, DocumentType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        bool removed = await _assignmentSut.UnassignAsync(DocumentType, targetId,
            TestContext.Current.CancellationToken);
        removed.ShouldBeTrue();

        Category? got = await _assignmentSut.GetForTargetAsync(DocumentType, targetId,
            TestContext.Current.CancellationToken);
        got.ShouldBeNull();
    }

    private sealed class TestDbContextFactory(DbContextOptions<TaxonomyDbContext> options)
        : IDbContextFactory<TaxonomyDbContext>
    {
        public TaxonomyDbContext CreateDbContext() => new(options);

        public Task<TaxonomyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<TaxonomyDbContext>(new(options));
    }

    private sealed class SimpleGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private sealed class CapturingLocalEventBus : ILocalEventBus
    {
        public List<object> Captured { get; } = [];

        public Task PublishAsync<TEvent>(TEvent localEvent, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            Captured.Add(localEvent);
            return Task.CompletedTask;
        }
    }
}
