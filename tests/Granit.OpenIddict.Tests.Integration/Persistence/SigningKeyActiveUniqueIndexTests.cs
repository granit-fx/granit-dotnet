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
/// Validates the filtered unique index that permits at most one <em>Active</em> signing/encryption
/// key per type — the database-level guard that makes concurrent first-boot key generation
/// race-safe. Needs real PostgreSQL because the partial-index filter (<c>WHERE "Status" = 'Active'</c>)
/// is not exercised by a mock or the SQLite in-memory provider used elsewhere.
/// </summary>
public sealed class SigningKeyActiveUniqueIndexTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private Factory? _factory;

    private EfSigningKeyStore Store => new(_factory!);

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();

        DbContextOptions<OpenIddictDbContext> options =
            new DbContextOptionsBuilder<OpenIddictDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .AddInterceptors(new ConcurrencyStampInterceptor())
                .Options;

        _factory = new Factory(options);

        await using OpenIddictDbContext db = _factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task SecondActiveKeyOfSameType_IsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await Store.CreateAsync(ActiveKey("signing"), ct);

        // A racing replica tries to persist a second active signing key — the partial unique index
        // must reject it so first-boot generation cannot mint two active keys.
        await Should.ThrowAsync<DbUpdateException>(() => Store.CreateAsync(ActiveKey("signing"), ct));
    }

    [Fact]
    public async Task ActiveAndRetiredOfSameType_Coexist()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        SigningKey first = ActiveKey("signing");
        await Store.CreateAsync(first, ct);
        SigningKey loaded = (await Store.GetActiveKeyAsync("signing", ct))!;
        loaded.Retire(DateTimeOffset.UnixEpoch.AddDays(1));
        await Store.UpdateAsync(loaded, ct);

        // The filter only constrains Active rows, so a new active key alongside the retired one is fine.
        await Should.NotThrowAsync(() => Store.CreateAsync(ActiveKey("signing"), ct));

        (await Store.GetKeysAsync([SigningKeyStatus.Active, SigningKeyStatus.Retired], ct)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ActiveKeysOfDifferentTypes_Coexist()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await Store.CreateAsync(ActiveKey("signing"), ct);

        // The uniqueness is per KeyType, so a signing and an encryption key are both active at once.
        await Should.NotThrowAsync(() => Store.CreateAsync(ActiveKey("encryption"), ct));
    }

    private sealed class Factory(DbContextOptions<OpenIddictDbContext> options)
        : IDbContextFactory<OpenIddictDbContext>
    {
        public OpenIddictDbContext CreateDbContext() => new(options, NullTenantContext.Instance);
    }

    private static SigningKey ActiveKey(string keyType) =>
        SigningKey.Create(
            keyId: $"{keyType}-{Guid.NewGuid():N}",
            keyType: keyType,
            algorithm: keyType == "signing" ? "RS256" : "RSA-OAEP",
            encryptedKeyMaterial: "encrypted-material",
            activatedAt: DateTimeOffset.UnixEpoch,
            expiresAt: DateTimeOffset.UnixEpoch.AddDays(90));
}

#pragma warning restore EF1001
