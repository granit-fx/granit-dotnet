using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests;

public sealed class EfCoreApiKeyStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly EfCoreApiKeyStore _sut;

    public EfCoreApiKeyStoreTests()
    {
        _factory = TestDbContextFactory.Create();
        _sut = new EfCoreApiKeyStore(_factory, NullLogger<EfCoreApiKeyStore>.Instance);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task FindByHashAsync_ExistingKey_ReturnsEntry()
    {
        await SeedAsync(CreateEntry("hash123"));

        ApiKeyEntry? result = await _sut.FindByHashAsync("hash123", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("Test Key");
    }

    [Fact]
    public async Task FindByHashAsync_NonExistentHash_ReturnsNull()
    {
        ApiKeyEntry? result = await _sut.FindByHashAsync("nonexistent", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByHashAsync_SoftDeletedKey_ReturnsNull()
    {
        ApiKeyEntry entry = CreateEntry("hash_deleted");
        entry.IsDeleted = true;
        entry.DeletedAt = DateTimeOffset.UtcNow;
        await SeedAsync(entry);

        ApiKeyEntry? result = await _sut.FindByHashAsync("hash_deleted", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateLastUsedAsync_UpdatesTimestamp()
    {
        ApiKeyEntry entry = CreateEntry("hash_used");
        await SeedAsync(entry);

        var usedAt = new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero);
        await _sut.UpdateLastUsedAsync(entry.Id, usedAt, TestContext.Current.CancellationToken);

        // Re-fetch with a fresh context to verify
        await using AuthenticationApiKeysDbContext db = _factory.CreateDbContext();
        ApiKeyEntry? updated = await db.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == entry.Id, TestContext.Current.CancellationToken);
        updated.ShouldNotBeNull();
        updated.LastUsedAt.ShouldBe(usedAt);
    }

    [Fact]
    public async Task UpdateLastUsedAsync_DbUpdateException_DoesNotPropagate()
    {
        // Mock factory that throws DbUpdateException when creating a context
        IDbContextFactory<AuthenticationApiKeysDbContext> mockFactory = Substitute.For<IDbContextFactory<AuthenticationApiKeysDbContext>>();
        mockFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns<AuthenticationApiKeysDbContext>(_ => throw new DbUpdateException("Simulated failure"));

        ILogger<EfCoreApiKeyStore> logger = Substitute.For<ILogger<EfCoreApiKeyStore>>();
        var failingSut = new EfCoreApiKeyStore(mockFactory, logger);

        // Should not throw — the exception is caught and logged
        await Should.NotThrowAsync(
            () => failingSut.UpdateLastUsedAsync(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken));
    }

    private async Task SeedAsync(ApiKeyEntry entry)
    {
        await using AuthenticationApiKeysDbContext db = _factory.CreateDbContext();
        db.ApiKeys.Add(entry);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ApiKeyEntry CreateEntry(string hash)
    {
        var entry = ApiKeyEntry.Create(
            Guid.NewGuid(),
            "Test Key",
            ApiKeyType.Secret,
            "test",
            hash,
            "gk_test_sk_",
            "abcd");
        entry.UpdatePermissions(["Read"]);
        entry.CreatedBy = "test";
        return entry;
    }

}
