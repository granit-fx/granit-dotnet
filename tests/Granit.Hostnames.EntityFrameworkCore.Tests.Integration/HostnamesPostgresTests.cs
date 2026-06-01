using Granit.Hostnames.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Postgres-backed integration tests for the Hostnames EF Core module. Exercises constraints
/// and behaviours that InMemory cannot model (unique index enforcement, cross-tenant resolution).
/// </summary>
[Collection(nameof(PostgresFixture))]
public sealed class HostnamesPostgresTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestHostnamesDbContext _context = null!;

    public HostnamesPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestHostnamesDbContext> options =
            new DbContextOptionsBuilder<TestHostnamesDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .Options;

        _context = new TestHostnamesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Table name uses the default prefix — tests always run with defaults.
        await _context.Database.ExecuteSqlAsync(
            $"TRUNCATE TABLE hostname_managed_hostnames RESTART IDENTITY CASCADE;",
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();

    // ── Unique host index — anti-hijacking ────────────────────────────────────

    [Fact]
    public async Task DuplicateHost_DifferentOwner_RejectedByUniqueIndex()
    {
        // Arrange: owner A claims acme.com
        var first = ManagedHostname.Create(
            Guid.NewGuid(), "acme.com", "cms.site",
            Guid.NewGuid(), tenantId: Guid.NewGuid());
        _context.ManagedHostnames.Add(first);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: owner B attempts to claim the same host
        var hijack = ManagedHostname.Create(
            Guid.NewGuid(), "acme.com", "cms.site",
            Guid.NewGuid(), tenantId: Guid.NewGuid());
        _context.ManagedHostnames.Add(hijack);

        DbUpdateException ex = await Should.ThrowAsync<DbUpdateException>(async () =>
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task SameOwner_MultipleHosts_AllPersisted()
    {
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        _context.ManagedHostnames.Add(ManagedHostname.Create(
            Guid.NewGuid(), "primary.acme.com", "cms.site", ownerId, tenantId, isPrimary: true));
        _context.ManagedHostnames.Add(ManagedHostname.Create(
            Guid.NewGuid(), "alias.acme.com", "cms.site", ownerId, tenantId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        int count = await _context.ManagedHostnames.AsNoTracking()
            .CountAsync(h => h.OwnerId == ownerId, TestContext.Current.CancellationToken);
        count.ShouldBe(2);
    }

    // ── Hostname SVO round-trip via Postgres ──────────────────────────────────

    [Fact]
    public async Task Hostname_SVO_RoundTrips_Through_Postgres()
    {
        var hostname = ManagedHostname.Create(
            Guid.NewGuid(), "SHOP.Example.COM", "cms.site",
            Guid.NewGuid(), tenantId: Guid.NewGuid());
        _context.ManagedHostnames.Add(hostname);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        ManagedHostname? loaded = await _context.ManagedHostnames.AsNoTracking()
            .FirstOrDefaultAsync(
                h => h.Id == hostname.Id,
                TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        // Hostname.Create normalises to lowercase
        loaded.Host.Value.ShouldBe("shop.example.com");
    }

    // ── Status enum persisted as string ──────────────────────────────────────

    [Fact]
    public async Task Status_PersistedAsString_NotInt()
    {
        var hostname = ManagedHostname.Create(
            Guid.NewGuid(), "status-check.example.com", "cms.site",
            Guid.NewGuid(), tenantId: Guid.NewGuid());
        _context.ManagedHostnames.Add(hostname);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // SqlQuery<string> uses a parameterised query (safe from SQL injection).
        // Column/table names use PascalCase — EF Core default without snake_case conventions.
        Guid id = hostname.Id;
        string? rawStatus = await _context.Database
            .SqlQuery<string>($"""SELECT "Status" AS "Value" FROM hostname_managed_hostnames WHERE "Id" = {id}""")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Create() sets Status = Pending; Active is only reached after DNS verification.
        rawStatus.ShouldBe("Pending");
    }
}
