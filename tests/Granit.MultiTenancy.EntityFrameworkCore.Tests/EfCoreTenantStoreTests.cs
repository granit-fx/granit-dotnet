using Granit.MultiTenancy.Domain;
using Granit.MultiTenancy.EntityFrameworkCore.Internal;
using Granit.MultiTenancy.Stores;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.EntityFrameworkCore.Tests;

/// <summary>
/// Tests for <see cref="EfCoreTenantStore"/>. Exercises every reader/writer path against an
/// in-memory EF Core provider — covers the EF-shaped behavior of <c>EfStoreBase.ReadAsync</c>/
/// <c>WriteAsync</c> on the multi-tenancy store, projection to <see cref="TenantData"/>, and
/// the domain mutation methods on <see cref="Tenant"/> invoked from the writer paths.
/// </summary>
public sealed class EfCoreTenantStoreTests : IDisposable
{
    private readonly InMemoryDbContextFactory _factory;
    private readonly EfCoreTenantStore _sut;

    public EfCoreTenantStoreTests()
    {
        _factory = new InMemoryDbContextFactory();
        _sut = new EfCoreTenantStore(_factory, NullLogger<EfCoreTenantStore>.Instance);
    }

    public void Dispose() => _factory.Dispose();

    private async Task SeedAsync(params Tenant[] tenants)
    {
        await using MultiTenancyDbContext ctx = _factory.CreateDbContext();
        ctx.Tenants.AddRange(tenants);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();
    }

    // -------------------------------------------------------------------------
    // ITenantReader
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindByIdAsync_ReturnsTenantData_WhenTenantExists()
    {
        var id = Guid.NewGuid();
        await SeedAsync(Tenant.Create(id, "Acme", "acme", "ops@acme.com", "FR"));

        TenantData? result = await _sut.FindByIdAsync(id, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(id);
        result.Name.ShouldBe("Acme");
        result.Identifier.ShouldBe("acme");
        result.ContactEmail.ShouldBe("ops@acme.com");
        result.Jurisdiction.ShouldBe("FR");
        result.Activated.ShouldBeTrue();
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenTenantDoesNotExist()
    {
        TenantData? result = await _sut.FindByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdentifierAsync_ReturnsTenantData_WhenIdentifierMatches()
    {
        await SeedAsync(Tenant.Create(Guid.NewGuid(), "Acme", "acme-corp"));

        TenantData? result = await _sut.FindByIdentifierAsync("acme-corp", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Identifier.ShouldBe("acme-corp");
    }

    [Fact]
    public async Task FindByIdentifierAsync_ReturnsNull_WhenIdentifierUnknown()
    {
        TenantData? result = await _sut.FindByIdentifierAsync("ghost", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindByIdentifierAsync_Throws_WhenIdentifierIsBlank(string identifier)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.FindByIdentifierAsync(identifier, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindByIdentifierAsync_ThrowsArgumentNullException_WhenIdentifierIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.FindByIdentifierAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoTenants()
    {
        IReadOnlyList<TenantData> result = await _sut.GetAllAsync(TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllTenantsSortedByName()
    {
        await SeedAsync(
            Tenant.Create(Guid.NewGuid(), "Charlie", "charlie"),
            Tenant.Create(Guid.NewGuid(), "Alpha", "alpha"),
            Tenant.Create(Guid.NewGuid(), "Bravo", "bravo"));

        IReadOnlyList<TenantData> result = await _sut.GetAllAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);
        result.Select(t => t.Name).ShouldBe(["Alpha", "Bravo", "Charlie"]);
    }

    [Fact]
    public async Task FindByCustomDomainAsync_ReturnsTenant_WhenCustomDomainMatches()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Acme", "acme");
        tenant.SetCustomDomain("app.acme-corp.com");
        await SeedAsync(tenant);

        TenantData? result = await _sut.FindByCustomDomainAsync(
            "app.acme-corp.com", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.CustomDomain.ShouldBe("app.acme-corp.com");
    }

    [Fact]
    public async Task FindByCustomDomainAsync_ReturnsNull_WhenCustomDomainUnknown()
    {
        TenantData? result = await _sut.FindByCustomDomainAsync(
            "nobody.example.com", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task FindByCustomDomainAsync_Throws_WhenDomainIsBlank(string customDomain)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.FindByCustomDomainAsync(customDomain, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenTenantExists()
    {
        var id = Guid.NewGuid();
        await SeedAsync(Tenant.Create(id, "Acme", "acme"));

        bool exists = await _sut.ExistsAsync(id, TestContext.Current.CancellationToken);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenTenantMissing()
    {
        bool exists = await _sut.ExistsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        exists.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // ITenantWriter
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistsActiveTenant()
    {
        var id = Guid.NewGuid();

        await _sut.CreateAsync(
            id, "Acme", "acme", "ops@acme.com", "FR", TestContext.Current.CancellationToken);

        TenantData? stored = await _sut.FindByIdAsync(id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Name.ShouldBe("Acme");
        stored.Identifier.ShouldBe("acme");
        stored.ContactEmail.ShouldBe("ops@acme.com");
        stored.Jurisdiction.ShouldBe("FR");
        stored.Activated.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesNameAndContactAndJurisdiction()
    {
        var id = Guid.NewGuid();
        await SeedAsync(Tenant.Create(id, "Acme", "acme", "old@acme.com", "FR"));

        await _sut.UpdateAsync(
            id, "Acme International", "ops@acme.com", "BE", TestContext.Current.CancellationToken);

        TenantData? stored = await _sut.FindByIdAsync(id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Name.ShouldBe("Acme International");
        stored.ContactEmail.ShouldBe("ops@acme.com");
        stored.Jurisdiction.ShouldBe("BE");
        // Identifier is intentionally immutable through UpdateAsync.
        stored.Identifier.ShouldBe("acme");
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenTenantMissing()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(
                Guid.NewGuid(), "Anything", null, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ActivateAsync_SetsActivatedTrue_WhenPreviouslyDeactivated()
    {
        var id = Guid.NewGuid();
        var tenant = Tenant.Create(id, "Acme", "acme");
        tenant.Deactivate();
        await SeedAsync(tenant);

        await _sut.ActivateAsync(id, TestContext.Current.CancellationToken);

        TenantData? stored = await _sut.FindByIdAsync(id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Activated.ShouldBeTrue();
    }

    [Fact]
    public async Task ActivateAsync_IsIdempotent_WhenAlreadyActive()
    {
        var id = Guid.NewGuid();
        await SeedAsync(Tenant.Create(id, "Acme", "acme"));

        await _sut.ActivateAsync(id, TestContext.Current.CancellationToken);

        TenantData? stored = await _sut.FindByIdAsync(id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Activated.ShouldBeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_SetsActivatedFalse()
    {
        var id = Guid.NewGuid();
        await SeedAsync(Tenant.Create(id, "Acme", "acme"));

        await _sut.DeactivateAsync(id, TestContext.Current.CancellationToken);

        TenantData? stored = await _sut.FindByIdAsync(id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Activated.ShouldBeFalse();
    }

    [Fact]
    public async Task DeactivateAsync_Throws_WhenTenantMissing()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.DeactivateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // Test harness
    // -------------------------------------------------------------------------

    /// <summary>
    /// Minimal <see cref="IDbContextFactory{T}"/> over EF Core InMemory. Each instance shares a
    /// database name so all factory-produced contexts hit the same store (mimicking the
    /// connection-pooled production factory). Disposed at the end of the test.
    /// </summary>
    private sealed class InMemoryDbContextFactory : IDbContextFactory<MultiTenancyDbContext>, IDisposable
    {
        private readonly DbContextOptions<MultiTenancyDbContext> _options;

        public InMemoryDbContextFactory()
        {
            _options = new DbContextOptionsBuilder<MultiTenancyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        public MultiTenancyDbContext CreateDbContext() =>
            new(_options, GranitDesignTime.CurrentTenant);

        public void Dispose()
        {
            using MultiTenancyDbContext ctx = CreateDbContext();
            ctx.Database.EnsureDeleted();
        }
    }
}
