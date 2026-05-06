using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// SQLite-backed tests for <see cref="TagService"/>. Exercises the full CRUD surface
/// of T1.2 plus the <c>(TenantId, Scope, Name)</c> uniqueness constraint enforced by
/// the <c>ux_taxonomy_tags_tenant_scope_name</c> index.
/// </summary>
public sealed class TagServiceSqliteTests : IAsyncLifetime
{
    private SqliteConnection _holdOpen = null!;
    private TestDbContextFactory _factory = null!;
    private TagService _sut = null!;

    private static readonly Guid TenantId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        string connectionString =
            $"DataSource=file:taxonomy-{Guid.NewGuid():N}?mode=memory&cache=shared";
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

        _sut = new TagService(_factory, currentTenant, new SimpleGuidGenerator());
    }

    public ValueTask DisposeAsync() => _holdOpen.DisposeAsync();

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistsTag()
    {
        Tag tag = await _sut.CreateAsync(
            "documents", "urgent", "#FF0000", cancellationToken: TestContext.Current.CancellationToken);

        tag.Id.ShouldNotBe(Guid.Empty);
        tag.TenantId.ShouldBe(TenantId);
        tag.Scope.ShouldBe("documents");
        tag.Name.ShouldBe("urgent");
        tag.Color.ShouldBe("#FF0000");

        await using TaxonomyDbContext context = await _factory.CreateDbContextAsync(
            TestContext.Current.CancellationToken);
        Tag? loaded = await context.Tags.FindAsync([tag.Id], TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("urgent");
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameInSameScope_Throws()
    {
        await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.CreateAsync("documents", "urgent", "#00FF00",
                cancellationToken: TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("urgent");
        ex.Message.ShouldContain("documents");
    }

    [Fact]
    public async Task CreateAsync_SameNameDifferentScope_Allowed()
    {
        Tag a = await _sut.CreateAsync("documents", "vip", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        Tag b = await _sut.CreateAsync("parties", "vip", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);

        a.Id.ShouldNotBe(b.Id);
        a.Scope.ShouldBe("documents");
        b.Scope.ShouldBe("parties");
    }

    // -------------------------------------------------------------------------
    // RenameAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenameAsync_UpdatesNameAndBumpsRowVersion()
    {
        Tag created = await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);

        Tag? renamed = await _sut.RenameAsync(created.Id, "critical",
            cancellationToken: TestContext.Current.CancellationToken);

        renamed.ShouldNotBeNull();
        renamed.Name.ShouldBe("critical");
        renamed.RowVersion.ShouldBe(2u);
    }

    [Fact]
    public async Task RenameAsync_UnknownId_ReturnsNull()
    {
        Tag? result = await _sut.RenameAsync(Guid.NewGuid(), "anything",
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // RecolourAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RecolourAsync_UpdatesColor()
    {
        Tag created = await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);

        Tag? recoloured = await _sut.RecolourAsync(created.Id, "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);

        recoloured.ShouldNotBeNull();
        recoloured.Color.ShouldBe("#00FF00");
    }

    // -------------------------------------------------------------------------
    // ToggleHideAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ToggleHideAsync_FlipsFlag()
    {
        Tag created = await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        created.HideOnEntityCard.ShouldBeFalse();

        Tag? toggled = await _sut.ToggleHideAsync(created.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        toggled.ShouldNotBeNull();
        toggled.HideOnEntityCard.ShouldBeTrue();

        Tag? toggledBack = await _sut.ToggleHideAsync(created.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        toggledBack.ShouldNotBeNull();
        toggledBack.HideOnEntityCard.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesRowAndReturnsTrue()
    {
        Tag created = await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);

        bool deleted = await _sut.DeleteAsync(created.Id, TestContext.Current.CancellationToken);
        deleted.ShouldBeTrue();

        Tag? loaded = await _sut.GetByIdAsync(created.Id, TestContext.Current.CancellationToken);
        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        bool deleted = await _sut.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        deleted.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ReturnsTag()
    {
        Tag created = await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);

        Tag? loaded = await _sut.GetByIdAsync(created.Id, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(created.Id);
    }

    // -------------------------------------------------------------------------
    // ListByScopeAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListByScopeAsync_FiltersByScope()
    {
        await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        await _sut.CreateAsync("documents", "review", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);
        await _sut.CreateAsync("parties", "vip", "#0000FF",
            cancellationToken: TestContext.Current.CancellationToken);

        IReadOnlyList<Tag> docTags = await _sut.ListByScopeAsync(
            "documents", cancellationToken: TestContext.Current.CancellationToken);

        docTags.Count.ShouldBe(2);
        docTags.Select(t => t.Name).ShouldBe(["review", "urgent"]); // ordered by name
    }

    [Fact]
    public async Task ListByScopeAsync_StarScope_ReturnsAllTags()
    {
        await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        await _sut.CreateAsync("parties", "vip", "#0000FF",
            cancellationToken: TestContext.Current.CancellationToken);

        IReadOnlyList<Tag> all = await _sut.ListByScopeAsync(
            "*", cancellationToken: TestContext.Current.CancellationToken);

        all.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListByScopeAsync_NamePrefixFilter_AppliedCaseInsensitively()
    {
        await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        await _sut.CreateAsync("documents", "review", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);
        await _sut.CreateAsync("documents", "URGENT-fix", "#0000FF",
            cancellationToken: TestContext.Current.CancellationToken);

        IReadOnlyList<Tag> matches = await _sut.ListByScopeAsync(
            "documents", q: "urg", cancellationToken: TestContext.Current.CancellationToken);

        matches.Count.ShouldBe(2); // urgent + URGENT-fix (LIKE is case-insensitive in SQLite by default)
    }

    [Fact]
    public async Task ListByScopeAsync_PaginationApplied()
    {
        for (int i = 0; i < 5; i++)
        {
            await _sut.CreateAsync(
                "documents", $"tag-{i}", "#FF0000",
                cancellationToken: TestContext.Current.CancellationToken);
        }

        IReadOnlyList<Tag> page = await _sut.ListByScopeAsync(
            "documents", skip: 1, take: 2, cancellationToken: TestContext.Current.CancellationToken);

        page.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListByScopeAsync_InvalidTake_Throws()
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            _sut.ListByScopeAsync("documents", take: 0,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

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
}
