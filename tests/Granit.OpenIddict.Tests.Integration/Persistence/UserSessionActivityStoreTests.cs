using Granit.MultiTenancy;
using Granit.OpenIddict.EntityFrameworkCore.Entities;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // EfUserSessionActivityStore / OpenIddictDbContext are internal — accessible via InternalsVisibleTo

namespace Granit.OpenIddict.Tests.Integration.Persistence;

/// <summary>
/// Validates <see cref="EfUserSessionActivityStore"/> against real PostgreSQL — the SQL debounce and
/// the batched activity read cannot be exercised by a mock.
/// </summary>
public sealed class UserSessionActivityStoreTests : IAsyncLifetime
{
    private const string UserId = "user-activity";
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch.AddHours(1);

    private readonly PostgresFixture _postgres = new();
    private Factory? _factory;

    private EfUserSessionActivityStore Store => new(_factory!);

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();
        DbContextOptions<OpenIddictDbContext> options =
            new DbContextOptionsBuilder<OpenIddictDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .Options;
        _factory = new Factory(options);

        await using OpenIddictDbContext db = _factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Touch_StampsActivity_ThenDebouncesWithinWindow_ThenUpdatesAfterIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (Guid authId, Guid tokenId) = await SeedRefreshTokenAsync(ct);
        var debounce = TimeSpan.FromMinutes(1);

        await Store.TouchAsync(authId, T0, debounce, ct);
        (await Store.GetActivitiesAsync(UserId, ct))[tokenId.ToString()].ShouldBe(T0);

        // Within the debounce window — the stored value must not move.
        await Store.TouchAsync(authId, T0.AddSeconds(30), debounce, ct);
        (await Store.GetActivitiesAsync(UserId, ct))[tokenId.ToString()].ShouldBe(T0);

        // Past the window — the stored value advances.
        DateTimeOffset later = T0.AddMinutes(2);
        await Store.TouchAsync(authId, later, debounce, ct);
        (await Store.GetActivitiesAsync(UserId, ct))[tokenId.ToString()].ShouldBe(later);
    }

    [Fact]
    public async Task GetActivities_OnlyReturnsTouchedRefreshTokens()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (Guid authId, Guid tokenId) = await SeedRefreshTokenAsync(ct);
        await SeedRefreshTokenAsync(ct); // a second session, never touched

        await Store.TouchAsync(authId, T0, TimeSpan.FromMinutes(1), ct);

        IReadOnlyDictionary<string, DateTimeOffset> activities = await Store.GetActivitiesAsync(UserId, ct);

        activities.Count.ShouldBe(1);
        activities.ShouldContainKey(tokenId.ToString());
    }

    private async Task<(Guid AuthId, Guid TokenId)> SeedRefreshTokenAsync(CancellationToken ct)
    {
        var authId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();

        await using OpenIddictDbContext db = _factory!.CreateDbContext();
        GranitOpenIddictAuthorization authorization = new()
        {
            Id = authId,
            Subject = UserId,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
        };
        GranitOpenIddictToken token = new()
        {
            Id = tokenId,
            Subject = UserId,
            Type = OpenIddictConstants.TokenTypeHints.RefreshToken,
            Status = OpenIddictConstants.Statuses.Valid,
            Authorization = authorization,
        };
        db.Add(authorization);
        db.Add(token);
        await db.SaveChangesAsync(ct);
        return (authId, tokenId);
    }

    private sealed class Factory(DbContextOptions<OpenIddictDbContext> options)
        : IDbContextFactory<OpenIddictDbContext>
    {
        public OpenIddictDbContext CreateDbContext() => new(options, NullTenantContext.Instance);
    }
}

#pragma warning restore EF1001
