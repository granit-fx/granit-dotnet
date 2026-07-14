using Granit.OpenIddict.BackgroundJobs.Services;
using Granit.Settings.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.OpenIddict.BackgroundJobs.Tests.Jobs;

public sealed class OpenIddictIdleSessionEnforcementHandlerTests
{
    private readonly IOpenIddictTokenManager _tokenManager = Substitute.For<IOpenIddictTokenManager>();
    private readonly IFusionCache _cache = Substitute.For<IFusionCache>();
    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();

    private IdleSessionEnforcementService CreateService() =>
        new(_tokenManager, _cache, _settingProvider,
            NullLogger<IdleSessionEnforcementService>.Instance);

    [Fact]
    public async Task ExecuteAsync_SettingNull_DoesNotCallTokenManager()
    {
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        _tokenManager.DidNotReceive().ListAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SettingZero_ReturnsEarly()
    {
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns("0");

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        _tokenManager.DidNotReceive().ListAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SettingNegative_ReturnsEarly()
    {
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns("-5");

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        _tokenManager.DidNotReceive().ListAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SettingEnabled_EmptyTokenList_DoesNotRevoke()
    {
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns("30");

        _tokenManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(EmptyAsync());

        await Should.NotThrowAsync(() =>
            CreateService().ExecuteAsync(TestContext.Current.CancellationToken));

        await _tokenManager.DidNotReceive().TryRevokeAsync(
            Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SettingEnabled_IdleRefreshToken_DoesNotRevoke()
    {
        // Safe-guard: even when a refresh token has no session cache entry (which the broken
        // key contract makes universal), the job must NOT revoke — otherwise every session is
        // logged out on the first run.
        object token = new();
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns("30");
        _tokenManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(SingleAsync(token));
        _tokenManager.GetTypeAsync(token, Arg.Any<CancellationToken>())
            .Returns(OpenIddictConstants.TokenTypeHints.RefreshToken);
        _tokenManager.GetSubjectAsync(token, Arg.Any<CancellationToken>()).Returns("user-1");
        _tokenManager.GetIdAsync(token, Arg.Any<CancellationToken>()).Returns("refresh-token-1");

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _tokenManager.DidNotReceive().TryRevokeAsync(
            Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<object> EmptyAsync()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<object> SingleAsync(object item)
    {
        await Task.CompletedTask;
        yield return item;
    }
}
