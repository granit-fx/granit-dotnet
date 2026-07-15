using Granit.OpenIddict.BackgroundJobs.Services;
using Granit.OpenIddict.EntityFrameworkCore.Entities;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Granit.Settings.Services;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // OpenIddictDbContext / EfUserSessionActivityStore are internal — accessible via InternalsVisibleTo

namespace Granit.OpenIddict.Tests.Integration.Jobs;

/// <summary>
/// End-to-end validation of the idle-session job against real PostgreSQL and a real
/// <see cref="IOpenIddictTokenManager"/>: an idle refresh token is revoked, a remember-me session is
/// exempt, and an active session survives. This exercises the full path the unit tests mock —
/// the SQL idle query, the manager's <c>remember_me</c> property read, and <c>TryRevokeAsync</c>.
/// </summary>
[Collection("openiddict-integration")]
public sealed class IdleSessionEnforcementServiceTests(OpenIddictTestApplication app)
{
    private const int TimeoutMinutes = 30;
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddYears(56);
    private static readonly DateTimeOffset Idle = Now.AddMinutes(-(TimeoutMinutes + 5));
    private static readonly DateTimeOffset Recent = Now.AddMinutes(-1);

    [Fact]
    public async Task ExecuteAsync_RevokesIdle_ExemptsRememberMe_KeepsActive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        Guid idleId = await SeedRefreshTokenAsync(Idle, rememberMe: false, ct);
        Guid rememberId = await SeedRefreshTokenAsync(Idle, rememberMe: true, ct);
        Guid activeId = await SeedRefreshTokenAsync(Recent, rememberMe: false, ct);

        await CreateService().ExecuteAsync(ct);

        (await StatusAsync(idleId, ct)).ShouldBe(OpenIddictConstants.Statuses.Revoked);
        (await StatusAsync(rememberId, ct)).ShouldBe(OpenIddictConstants.Statuses.Valid);
        (await StatusAsync(activeId, ct)).ShouldBe(OpenIddictConstants.Statuses.Valid);
    }

    private IdleSessionEnforcementService CreateService()
    {
        IOpenIddictTokenManager tokenManager =
            app.Services.GetRequiredService<IOpenIddictTokenManager>();

        ISettingProvider settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns(TimeoutMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture));

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        return new IdleSessionEnforcementService(
            tokenManager, new EfUserSessionActivityStore(DbFactory), settingProvider, clock,
            NullLogger<IdleSessionEnforcementService>.Instance);
    }

    private async Task<Guid> SeedRefreshTokenAsync(
        DateTimeOffset lastActivityAt, bool rememberMe, CancellationToken ct)
    {
        var authId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();

        await using OpenIddictDbContext db = await DbFactory.CreateDbContextAsync(ct);
        GranitOpenIddictAuthorization authorization = new()
        {
            Id = authId,
            Subject = $"idle-user-{tokenId:N}",
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
        };
        GranitOpenIddictToken token = new()
        {
            Id = tokenId,
            Subject = authorization.Subject,
            Type = OpenIddictConstants.TokenTypeHints.RefreshToken,
            Status = OpenIddictConstants.Statuses.Valid,
            Authorization = authorization,
            LastActivityAt = lastActivityAt,
            Properties = rememberMe ? "{\"remember_me\":\"true\"}" : null,
        };
        db.Add(authorization);
        db.Add(token);
        await db.SaveChangesAsync(ct);
        return tokenId;
    }

    private async Task<string?> StatusAsync(Guid tokenId, CancellationToken ct)
    {
        await using OpenIddictDbContext db = await DbFactory.CreateDbContextAsync(ct);
        return await db.Set<GranitOpenIddictToken>()
            .Where(t => t.Id == tokenId)
            .Select(t => t.Status)
            .SingleAsync(ct);
    }

    private IDbContextFactory<OpenIddictDbContext> DbFactory =>
        app.Services.GetRequiredService<IDbContextFactory<OpenIddictDbContext>>();
}

#pragma warning restore EF1001
