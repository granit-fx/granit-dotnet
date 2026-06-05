using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests;

public sealed class EfCoreApiKeyAdminStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly EfCoreApiKeyAdminStore _sut;

    public EfCoreApiKeyAdminStoreTests()
    {
        _factory = TestDbContextFactory.Create();
        _sut = new EfCoreApiKeyAdminStore(_factory, Substitute.For<ICurrentTenant>());
    }

    public void Dispose() => _factory.Dispose();

    // --- FindByIdAsync ---

    [Fact]
    public async Task FindByIdAsync_ExistingKey_ReturnsEntry()
    {
        ApiKeyEntry entry = CreateEntry();
        await SeedAsync(entry);

        ApiKeyEntry? result = await _sut.FindByIdAsync(entry.Id, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(entry.Id);
        result.Name.ShouldBe(entry.Name);
    }

    [Fact]
    public async Task FindByIdAsync_NonExistentId_ReturnsNull()
    {
        ApiKeyEntry? result = await _sut.FindByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_PersistsEntry()
    {
        ApiKeyEntry entry = CreateEntry();

        await _sut.CreateAsync(entry, TestContext.Current.CancellationToken);

        await using AuthenticationApiKeysDbContext db = _factory.CreateDbContext();
        ApiKeyEntry? persisted = await db.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == entry.Id, TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe(entry.Name);
        persisted.HashedKey.ShouldBe(entry.HashedKey);
    }

    [Fact]
    public async Task CreateAsync_NullEntry_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.CreateAsync(null!, TestContext.Current.CancellationToken));
    }

    // --- RevokeAsync ---

    [Fact]
    public async Task RevokeAsync_ExistingActiveKey_ReturnsTrue()
    {
        ApiKeyEntry entry = CreateEntry();
        await SeedAsync(entry);
        var revokedAt = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

        bool result = await _sut.RevokeAsync(entry.Id, revokedAt, TestContext.Current.CancellationToken);

        result.ShouldBeTrue();

        await using AuthenticationApiKeysDbContext db = _factory.CreateDbContext();
        ApiKeyEntry? updated = await db.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == entry.Id, TestContext.Current.CancellationToken);
        updated.ShouldNotBeNull();
        updated.RevokedAt.ShouldBe(revokedAt);
    }

    [Fact]
    public async Task RevokeAsync_AlreadyRevokedKey_ReturnsFalse()
    {
        ApiKeyEntry entry = CreateEntry();
        entry.Revoke(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedAsync(entry);

        bool result = await _sut.RevokeAsync(
            entry.Id,
            new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeAsync_NonExistentId_ReturnsFalse()
    {
        bool result = await _sut.RevokeAsync(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // --- UpdateScopesAsync ---

    [Fact]
    public async Task UpdateScopesAsync_ExistingKey_UpdatesAndReturnsTrue()
    {
        ApiKeyEntry entry = CreateEntry();
        await SeedAsync(entry);

        var newPermissions = new List<string> { "Read", "Write" };
        var newCidrs = new List<string> { "10.0.0.0/8" };

        bool result = await _sut.UpdateScopesAsync(
            entry.Id, newPermissions, newCidrs, TestContext.Current.CancellationToken);

        result.ShouldBeTrue();

        await using AuthenticationApiKeysDbContext db = _factory.CreateDbContext();
        ApiKeyEntry? updated = await db.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == entry.Id, TestContext.Current.CancellationToken);
        updated.ShouldNotBeNull();
        updated.Permissions.ShouldBe(newPermissions);
        updated.AllowedCidrs.ShouldBe(newCidrs);
    }

    [Fact]
    public async Task UpdateScopesAsync_NonExistentId_ReturnsFalse()
    {
        bool result = await _sut.UpdateScopesAsync(
            Guid.NewGuid(),
            ["Read"],
            [],
            TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // Listing/pagination moved to the query engine — see ApiKeyEntryQueryDefinitionTests.

    // --- Helpers ---

    private async Task SeedAsync(ApiKeyEntry entry)
    {
        await using AuthenticationApiKeysDbContext db = _factory.CreateDbContext();
        db.ApiKeys.Add(entry);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ApiKeyEntry CreateEntry(
        string hash = "default_hash",
        string name = "Test Key",
        ApiKeyType type = ApiKeyType.Secret,
        string environment = "test")
    {
        var entry = ApiKeyEntry.Create(
            Guid.NewGuid(),
            name,
            type,
            environment,
            hash,
            "gk_test_sk_",
            "abcd");
        entry.UpdatePermissions(["Read"]);
        entry.CreatedBy = "test";
        return entry;
    }
}
