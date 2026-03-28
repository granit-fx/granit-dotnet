using Granit.OpenIddict;
using Granit.OpenIddict.BackgroundJobs.Jobs;
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

    [Fact]
    public async Task HandleAsync_SettingNull_DoesNotCallTokenManager()
    {
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        await OpenIddictIdleSessionEnforcementHandler.HandleAsync(
            new OpenIddictIdleSessionEnforcementJob(),
            _tokenManager,
            _cache,
            _settingProvider,
            NullLogger<OpenIddictIdleSessionEnforcementJob>.Instance,
            TestContext.Current.CancellationToken);

        _tokenManager.DidNotReceive().ListAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SettingZero_ReturnsEarly()
    {
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns("0");

        await OpenIddictIdleSessionEnforcementHandler.HandleAsync(
            new OpenIddictIdleSessionEnforcementJob(),
            _tokenManager,
            _cache,
            _settingProvider,
            NullLogger<OpenIddictIdleSessionEnforcementJob>.Instance,
            TestContext.Current.CancellationToken);

        _tokenManager.DidNotReceive().ListAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SettingNegative_ReturnsEarly()
    {
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns("-5");

        await OpenIddictIdleSessionEnforcementHandler.HandleAsync(
            new OpenIddictIdleSessionEnforcementJob(),
            _tokenManager,
            _cache,
            _settingProvider,
            NullLogger<OpenIddictIdleSessionEnforcementJob>.Instance,
            TestContext.Current.CancellationToken);

        _tokenManager.DidNotReceive().ListAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SettingEnabled_EmptyTokenList_DoesNotRevoke()
    {
        _settingProvider.GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, Arg.Any<CancellationToken>())
            .Returns("30");

        _tokenManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(EmptyAsync());

        await Should.NotThrowAsync(() => OpenIddictIdleSessionEnforcementHandler.HandleAsync(
            new OpenIddictIdleSessionEnforcementJob(),
            _tokenManager,
            _cache,
            _settingProvider,
            NullLogger<OpenIddictIdleSessionEnforcementJob>.Instance,
            TestContext.Current.CancellationToken));

        await _tokenManager.DidNotReceive().TryRevokeAsync(
            Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<object> EmptyAsync()
    {
        await Task.CompletedTask;
        yield break;
    }
}
