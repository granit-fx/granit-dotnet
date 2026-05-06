using System.Diagnostics.Metrics;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// PostgreSQL-backed tests for the <c>(TenantId, Scope, Name)</c> uniqueness constraint
/// — the load-bearing invariant of ADR-054. Postgres uses case-sensitive equality on
/// text columns by default; the corresponding case-insensitive lookup behaviour is
/// exercised by the SQLite suite where SQLite's default LIKE collation is
/// case-insensitive.
/// </summary>
public sealed class TagPostgresUniquenessTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private DbContextOptions<TaxonomyDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private TagService _sut = null!;

    private static readonly Guid TenantId = Guid.NewGuid();

    public TagPostgresUniquenessTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<TaxonomyDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using TaxonomyDbContext init = new(_options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync("TRUNCATE TABLE taxonomy_tags RESTART IDENTITY CASCADE;");

        _factory = new TestDbContextFactory(_options);

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(TenantId);

        ServiceCollection services = new();
        services.AddMetrics();
        var metrics = new TaxonomyMetrics(
            services.BuildServiceProvider().GetRequiredService<IMeterFactory>());

        _sut = new TagService(_factory, currentTenant, new SimpleGuidGenerator(), metrics);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task UniqueConstraint_PreventsDuplicateNameInSameScope()
    {
        await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.CreateAsync("documents", "urgent", "#00FF00",
                cancellationToken: TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("urgent");
    }

    [Fact]
    public async Task UniqueConstraint_NameIsCaseSensitiveOnPostgres()
    {
        // Postgres text equality is case-sensitive, so "URGENT" and "urgent" are
        // distinct rows under the unique index. SQLite's LIKE-default-CI behaviour
        // is exercised in the unit-test suite.
        Tag lower = await _sut.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        Tag upper = await _sut.CreateAsync("documents", "URGENT", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);

        lower.Id.ShouldNotBe(upper.Id);
    }

    [Fact]
    public async Task UniqueConstraint_AllowsSameNameAcrossScopes()
    {
        Tag inDocs = await _sut.CreateAsync("documents", "vip", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        Tag inParties = await _sut.CreateAsync("parties", "vip", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);

        inDocs.Id.ShouldNotBe(inParties.Id);
    }

    [Fact]
    public async Task UniqueConstraint_RaceBetweenPreCheckAndInsert_SurfacesAsDbUpdateException()
    {
        // Insert a row directly via the DbContext (bypassing the service's pre-check),
        // then attempt a second insert that races past the pre-check window. EF Core
        // surfaces the unique-index violation as DbUpdateException carrying the
        // PostgresException as InnerException with SqlState 23505 (unique_violation).
        await using TaxonomyDbContext context = await _factory.CreateDbContextAsync(
            TestContext.Current.CancellationToken);
        var first = Tag.Create(Guid.NewGuid(), TenantId, "documents", "racing", "#000000");
        context.Tags.Add(first);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using TaxonomyDbContext other = await _factory.CreateDbContextAsync(
            TestContext.Current.CancellationToken);
        var duplicate = Tag.Create(Guid.NewGuid(), TenantId, "documents", "racing", "#FFFFFF");
        other.Tags.Add(duplicate);

        DbUpdateException ex = await Should.ThrowAsync<DbUpdateException>(() =>
            other.SaveChangesAsync(TestContext.Current.CancellationToken));
        var pgEx = ex.InnerException as PostgresException;
        pgEx.ShouldNotBeNull();
        pgEx.SqlState.ShouldBe("23505");
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
}
