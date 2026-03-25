using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
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
        _sut = new EfCoreApiKeyAdminStore(_factory);
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

    // --- ListAsync ---

    [Fact]
    public async Task ListAsync_NoFilters_ReturnsAllActiveKeys()
    {
        await SeedAsync(CreateEntry("hash1", "Key Alpha"));
        await SeedAsync(CreateEntry("hash2", "Key Beta"));

        PagedResult<ApiKeyEntry> result = await _sut.ListAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(2);
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_SearchFilter_MatchesByName()
    {
        await SeedAsync(CreateEntry("hash1", "Alpha Service"));
        await SeedAsync(CreateEntry("hash2", "Beta Gateway"));

        PagedResult<ApiKeyEntry> result = await _sut.ListAsync(
            search: "Alpha",
            cancellationToken: TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Name.ShouldBe("Alpha Service");
    }

    [Fact]
    public async Task ListAsync_TypeFilter_MatchesByType()
    {
        await SeedAsync(CreateEntry("hash1", "Secret Key", ApiKeyType.Secret));
        await SeedAsync(CreateEntry("hash2", "Webhook Key", ApiKeyType.Webhook));

        PagedResult<ApiKeyEntry> result = await _sut.ListAsync(
            type: ApiKeyType.Webhook,
            cancellationToken: TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Name.ShouldBe("Webhook Key");
    }

    [Fact]
    public async Task ListAsync_EnvironmentFilter_MatchesByEnvironment()
    {
        await SeedAsync(CreateEntry("hash1", "Live Key", environment: "live"));
        await SeedAsync(CreateEntry("hash2", "Test Key", environment: "test"));

        PagedResult<ApiKeyEntry> result = await _sut.ListAsync(
            environment: "live",
            cancellationToken: TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Name.ShouldBe("Live Key");
    }

    [Fact]
    public async Task ListAsync_ExcludesRevokedByDefault()
    {
        await SeedAsync(CreateEntry("hash1", "Active Key"));
        ApiKeyEntry revoked = CreateEntry("hash2", "Revoked Key");
        revoked.Revoke(DateTimeOffset.UtcNow);
        await SeedAsync(revoked);

        PagedResult<ApiKeyEntry> result = await _sut.ListAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(1);
        result.Items.Single().Name.ShouldBe("Active Key");
    }

    [Fact]
    public async Task ListAsync_IncludeRevoked_ReturnsAll()
    {
        await SeedAsync(CreateEntry("hash1", "Active Key"));
        ApiKeyEntry revoked = CreateEntry("hash2", "Revoked Key");
        revoked.Revoke(DateTimeOffset.UtcNow);
        await SeedAsync(revoked);

        PagedResult<ApiKeyEntry> result = await _sut.ListAsync(
            includeRevoked: true,
            cancellationToken: TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_Pagination_RespectsPageAndPageSize()
    {
        for (int i = 0; i < 5; i++)
        {
            await SeedAsync(CreateEntry($"hash{i}", $"Key {i}"));
        }

        PagedResult<ApiKeyEntry> result = await _sut.ListAsync(
            page: 2,
            pageSize: 2,
            cancellationToken: TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(5);
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_OrdersByCreatedAtDescending()
    {
        ApiKeyEntry older = CreateEntry("hash1", "Older Key");
        older.CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await SeedAsync(older);

        ApiKeyEntry newer = CreateEntry("hash2", "Newer Key");
        newer.CreatedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        await SeedAsync(newer);

        PagedResult<ApiKeyEntry> result = await _sut.ListAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.Items[0].Name.ShouldBe("Newer Key");
        result.Items[1].Name.ShouldBe("Older Key");
    }

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
