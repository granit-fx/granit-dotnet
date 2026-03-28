using Granit.OpenIddict.BackgroundJobs.Jobs;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.BackgroundJobs.Tests.Jobs;

public sealed class OpenIddictTokenCleanupHandlerTests
{
    [Fact]
    public async Task HandleAsync_CallsPruneOnBothManagers()
    {
        IOpenIddictTokenManager tokenManager = Substitute.For<IOpenIddictTokenManager>();
        IOpenIddictAuthorizationManager authorizationManager = Substitute.For<IOpenIddictAuthorizationManager>();
        TimeProvider timeProvider = TimeProvider.System;

        await OpenIddictTokenCleanupHandler.HandleAsync(
            new OpenIddictTokenCleanupJob(),
            tokenManager,
            authorizationManager,
            timeProvider,
            TestContext.Current.CancellationToken);

        await tokenManager.Received(1).PruneAsync(
            Arg.Any<DateTimeOffset>(),
            TestContext.Current.CancellationToken);

        await authorizationManager.Received(1).PruneAsync(
            Arg.Any<DateTimeOffset>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_DoesNotThrow()
    {
        IOpenIddictTokenManager tokenManager = Substitute.For<IOpenIddictTokenManager>();
        IOpenIddictAuthorizationManager authorizationManager = Substitute.For<IOpenIddictAuthorizationManager>();

        await Should.NotThrowAsync(() => OpenIddictTokenCleanupHandler.HandleAsync(
            new OpenIddictTokenCleanupJob(),
            tokenManager,
            authorizationManager,
            TimeProvider.System,
            TestContext.Current.CancellationToken));
    }
}
