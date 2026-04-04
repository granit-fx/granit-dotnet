using Granit.OpenIddict.BackgroundJobs.Internal;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.BackgroundJobs.Tests.Jobs;

public sealed class OpenIddictTokenCleanupHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_CallsPruneOnBothManagers()
    {
        IOpenIddictTokenManager tokenManager = Substitute.For<IOpenIddictTokenManager>();
        IOpenIddictAuthorizationManager authorizationManager = Substitute.For<IOpenIddictAuthorizationManager>();
        TimeProvider timeProvider = TimeProvider.System;

        var service = new TokenCleanupService(tokenManager, authorizationManager, timeProvider);
        await service.ExecuteAsync(TestContext.Current.CancellationToken);

        await tokenManager.Received(1).PruneAsync(
            Arg.Any<DateTimeOffset>(),
            TestContext.Current.CancellationToken);

        await authorizationManager.Received(1).PruneAsync(
            Arg.Any<DateTimeOffset>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotThrow()
    {
        IOpenIddictTokenManager tokenManager = Substitute.For<IOpenIddictTokenManager>();
        IOpenIddictAuthorizationManager authorizationManager = Substitute.For<IOpenIddictAuthorizationManager>();

        var service = new TokenCleanupService(tokenManager, authorizationManager, TimeProvider.System);

        await Should.NotThrowAsync(() =>
            service.ExecuteAsync(TestContext.Current.CancellationToken));
    }
}
