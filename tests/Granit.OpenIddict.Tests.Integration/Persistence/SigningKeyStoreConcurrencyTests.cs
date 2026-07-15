using Granit.MultiTenancy;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // EfSigningKeyStore / OpenIddictDbContext are internal — accessible via InternalsVisibleTo

namespace Granit.OpenIddict.Tests.Integration.Persistence;

/// <summary>
/// Exercises <see cref="EfSigningKeyStore"/> against real PostgreSQL — the optimistic-concurrency
/// gate that key rotation relies on cannot be validated with an in-memory or mocked store.
/// </summary>
public sealed class SigningKeyStoreConcurrencyTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private TestContextFactory? _factory;

    private EfSigningKeyStore Store => new(_factory!);

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();

        DbContextOptions<OpenIddictDbContext> options =
            new DbContextOptionsBuilder<OpenIddictDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .AddInterceptors(new ConcurrencyStampInterceptor())
                .Options;

        _factory = new TestContextFactory(options);

        await using OpenIddictDbContext db = _factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task CreateThenGetActiveKey_RoundTrips()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SigningKey key = NewActiveKey("signing");
        await Store.CreateAsync(key, ct);

        SigningKey? active = await Store.GetActiveKeyAsync("signing", ct);

        active.ShouldNotBeNull();
        active.KeyId.ShouldBe(key.KeyId);
        active.Status.ShouldBe(SigningKeyStatus.Active);
    }

    [Fact]
    public async Task ConcurrentUpdate_SecondWriterLosesTheRace()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SigningKey key = NewActiveKey("signing");
        await Store.CreateAsync(key, ct);

        // Two rotations load the same active key before either commits.
        SigningKey loadA = (await Store.GetActiveKeyAsync("signing", ct))!;
        SigningKey loadB = (await Store.GetActiveKeyAsync("signing", ct))!;

        loadA.Retire(DateTimeOffset.UnixEpoch.AddDays(10));
        bool firstWon = await Store.UpdateAsync(loadA, ct);

        loadB.Retire(DateTimeOffset.UnixEpoch.AddDays(10));
        bool secondWon = await Store.UpdateAsync(loadB, ct);

        firstWon.ShouldBeTrue("the first writer commits against the original concurrency stamp");
        secondWon.ShouldBeFalse("the second writer's stamp is stale — it must lose the race, not mint a duplicate");
    }

    [Fact]
    public async Task GetKeysAsync_FiltersByStatus()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await Store.CreateAsync(NewActiveKey("signing"), ct);

        SigningKey encryption = NewActiveKey("encryption");
        await Store.CreateAsync(encryption, ct);
        SigningKey loaded = (await Store.GetActiveKeyAsync("encryption", ct))!;
        loaded.Retire(DateTimeOffset.UnixEpoch.AddDays(1));
        await Store.UpdateAsync(loaded, ct);

        (await Store.GetKeysAsync([SigningKeyStatus.Active], ct)).ShouldHaveSingleItem();
        (await Store.GetKeysAsync([SigningKeyStatus.Retired], ct)).ShouldHaveSingleItem();
        (await Store.GetKeysAsync([SigningKeyStatus.Active, SigningKeyStatus.Retired], ct)).Count.ShouldBe(2);
    }

    private sealed class TestContextFactory(DbContextOptions<OpenIddictDbContext> options)
        : IDbContextFactory<OpenIddictDbContext>
    {
        public OpenIddictDbContext CreateDbContext() => new(options, NullTenantContext.Instance);
    }

    private static SigningKey NewActiveKey(string keyType) =>
        SigningKey.Create(
            keyId: $"{keyType}-{Guid.NewGuid():N}",
            keyType: keyType,
            algorithm: keyType == "signing" ? "RS256" : "RSA-OAEP",
            encryptedKeyMaterial: "encrypted-material",
            activatedAt: DateTimeOffset.UnixEpoch,
            expiresAt: DateTimeOffset.UnixEpoch.AddDays(90));
}

#pragma warning restore EF1001
