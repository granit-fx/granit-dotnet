using Granit.OpenIddict.BackgroundJobs.Internal;
using Granit.OpenIddict.BackgroundJobs.Jobs;
using Granit.OpenIddict.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.BackgroundJobs.Tests.Jobs;

public sealed class OpenIddictKeyRotationHandlerTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToKeyRotationService()
    {
        IKeyRotationService keyRotationService = Substitute.For<IKeyRotationService>();
        keyRotationService.RotateAsync(Arg.Any<CancellationToken>())
            .Returns(new KeyRotationResult(1, 1, 0, 0));
        var service = new KeyRotationExecutionService(
            keyRotationService, NullLogger<KeyRotationExecutionService>.Instance);

        await OpenIddictKeyRotationHandler.HandleAsync(
            new OpenIddictKeyRotationJob(),
            service,
            TestContext.Current.CancellationToken);

        await keyRotationService.Received(1).RotateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DoesNotThrow_WhenNoKeysRotated()
    {
        IKeyRotationService keyRotationService = Substitute.For<IKeyRotationService>();
        keyRotationService.RotateAsync(Arg.Any<CancellationToken>())
            .Returns(new KeyRotationResult(0, 0, 0, 0));
        var service = new KeyRotationExecutionService(
            keyRotationService, NullLogger<KeyRotationExecutionService>.Instance);

        await Should.NotThrowAsync(() => OpenIddictKeyRotationHandler.HandleAsync(
            new OpenIddictKeyRotationJob(),
            service,
            TestContext.Current.CancellationToken));
    }
}
