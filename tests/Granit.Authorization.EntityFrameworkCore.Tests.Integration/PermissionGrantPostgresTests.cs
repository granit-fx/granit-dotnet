using Granit.Authorization;
using Granit.Authorization.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Regression coverage for the <c>NULLS NOT DISTINCT</c> annotation applied to the
/// <c>authorization_permission_grants</c> unique index. Before the fix, two host-level
/// grants (<c>TenantId = null</c>) on the same <c>(ProviderName, ProviderKey, Name)</c>
/// tuple could silently duplicate — the index treated nulls as distinct per ANSI default.
/// </summary>
public sealed class PermissionGrantPostgresTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestAuthorizationDbContext _context = null!;

    public PermissionGrantPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestAuthorizationDbContext> options =
            new DbContextOptionsBuilder<TestAuthorizationDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .Options;

        _context = new TestAuthorizationDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        await _context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE authorization_role_metadata, authorization_permission_grants RESTART IDENTITY CASCADE;");
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();

    [Fact]
    public async Task HostGrant_DuplicateTuple_RejectedByUniqueIndex()
    {
        _context.Set<PermissionGrant>().Add(PermissionGrant.Create(
            Guid.NewGuid(), "Invoices.Delete",
            PermissionGrantProviderNames.Role, "accountant", tenantId: null));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _context.Set<PermissionGrant>().Add(PermissionGrant.Create(
            Guid.NewGuid(), "Invoices.Delete",
            PermissionGrantProviderNames.Role, "accountant", tenantId: null));

        DbUpdateException ex = await Should.ThrowAsync<DbUpdateException>(async () =>
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task SameTuple_DifferentTenants_BothPersisted()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _context.Set<PermissionGrant>().Add(PermissionGrant.Create(
            Guid.NewGuid(), "Invoices.Delete",
            PermissionGrantProviderNames.Role, "accountant", tenantA));
        _context.Set<PermissionGrant>().Add(PermissionGrant.Create(
            Guid.NewGuid(), "Invoices.Delete",
            PermissionGrantProviderNames.Role, "accountant", tenantB));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        int count = await _context.Set<PermissionGrant>().AsNoTracking()
            .CountAsync(g => g.Name == "Invoices.Delete" && g.ProviderKey == "accountant",
                TestContext.Current.CancellationToken);
        count.ShouldBe(2);
    }
}
