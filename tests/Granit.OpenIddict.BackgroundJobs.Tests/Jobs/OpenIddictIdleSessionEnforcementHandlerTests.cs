using System.Collections.Immutable;
using System.Text.Json;
using Granit.OpenIddict.BackgroundJobs.Services;
using Granit.OpenIddict.Services;
using Granit.Settings.Services;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIddict.Abstractions;
using Xunit;

namespace Granit.OpenIddict.BackgroundJobs.Tests.Jobs;

public sealed class OpenIddictIdleSessionEnforcementHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IOpenIddictTokenManager _tokenManager = Substitute.For<IOpenIddictTokenManager>();
    private readonly IUserSessionActivityStore _activityStore = Substitute.For<IUserSessionActivityStore>();
    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public OpenIddictIdleSessionEnforcementHandlerTests() => _clock.Now.Returns(Now);

    private IdleSessionEnforcementService CreateService() =>
        new(_tokenManager, _activityStore, _settingProvider, _clock,
            NullLogger<IdleSessionEnforcementService>.Instance);

    [Fact]
    public async Task ExecuteAsync_TimeoutUnset_DoesNotQueryIdleSessions()
    {
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _activityStore.DidNotReceive().GetIdleRefreshTokenIdsAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutZero_ReturnsEarly()
    {
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns("0");

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _activityStore.DidNotReceive().GetIdleRefreshTokenIdsAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_IdleSession_IsRevoked()
    {
        EnableWith(timeoutMinutes: 30);
        object token = SetupIdleToken("idle-token", rememberMe: false);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _tokenManager.Received(1).TryRevokeAsync(token, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RememberMeSession_IsExempt()
    {
        EnableWith(timeoutMinutes: 30);
        object token = SetupIdleToken("remember-token", rememberMe: true);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _tokenManager.DidNotReceive().TryRevokeAsync(token, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ComputesCutoffFromTimeout()
    {
        EnableWith(timeoutMinutes: 30);
        _activityStore.GetIdleRefreshTokenIdsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _activityStore.Received(1).GetIdleRefreshTokenIdsAsync(
            Now.AddMinutes(-30), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private void EnableWith(int timeoutMinutes) =>
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns(timeoutMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private object SetupIdleToken(string tokenId, bool rememberMe)
    {
        object token = new();
        _activityStore.GetIdleRefreshTokenIdsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([tokenId]);
        _tokenManager.FindByIdAsync(tokenId, Arg.Any<CancellationToken>())
            .Returns(token);

        ImmutableDictionary<string, JsonElement> properties = rememberMe
            ? ImmutableDictionary<string, JsonElement>.Empty.Add(
                "remember_me", JsonDocument.Parse("\"true\"").RootElement)
            : ImmutableDictionary<string, JsonElement>.Empty;
        _tokenManager.GetPropertiesAsync(token, Arg.Any<CancellationToken>())
            .Returns(properties);

        _tokenManager.TryRevokeAsync(token, Arg.Any<CancellationToken>())
            .Returns(true);
        return token;
    }
}
