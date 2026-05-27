using Granit.Features.Exceptions;
using Granit.Features.Wolverine.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Features.Wolverine.Tests;

public sealed class RequiresFeatureMiddlewareTests
{
    // Test message types
    [RequiresFeature("App.VideoConsultation")]
    private sealed class SingleFeatureMessage;

    [RequiresFeature("App.VideoConsultation")]
    [RequiresFeature("App.ExportPdf")]
    private sealed class MultiFeatureMessage;

    private sealed class NoFeatureMessage;

    // -------------------------------------------------------------------------
    // BeforeAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BeforeAsync_NoAttribute_DoesNotCallChecker()
    {
        IFeatureChecker checker = Substitute.For<IFeatureChecker>();

        await RequiresFeatureMiddleware.BeforeAsync(
            new NoFeatureMessage(),
            checker,
            TestContext.Current.CancellationToken);

        await checker.DidNotReceiveWithAnyArgs()
                     .RequireEnabledAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BeforeAsync_SingleFeatureEnabled_DoesNotThrow()
    {
        IFeatureChecker checker = Substitute.For<IFeatureChecker>();
        checker.RequireEnabledAsync("App.VideoConsultation", Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);

        Func<Task> act = () => RequiresFeatureMiddleware.BeforeAsync(
            new SingleFeatureMessage(),
            checker,
            TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
        await checker.Received(1)
                     .RequireEnabledAsync("App.VideoConsultation", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BeforeAsync_SingleFeatureDisabled_ThrowsFeatureNotEnabledException()
    {
        IFeatureChecker checker = Substitute.For<IFeatureChecker>();
        checker.RequireEnabledAsync("App.VideoConsultation", Arg.Any<CancellationToken>())
               .ThrowsAsync(new FeatureNotEnabledException("App.VideoConsultation"));

        Func<Task> act = () => RequiresFeatureMiddleware.BeforeAsync(
            new SingleFeatureMessage(),
            checker,
            TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<FeatureNotEnabledException>(act)).Message.ShouldContain("App.VideoConsultation");
    }

    [Fact]
    public async Task BeforeAsync_MultipleFeatures_AllEnabled_DoesNotThrow()
    {
        IFeatureChecker checker = Substitute.For<IFeatureChecker>();
        checker.RequireEnabledAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);

        Func<Task> act = () => RequiresFeatureMiddleware.BeforeAsync(
            new MultiFeatureMessage(),
            checker,
            TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
        await checker.Received(1)
                     .RequireEnabledAsync("App.VideoConsultation", Arg.Any<CancellationToken>());
        await checker.Received(1)
                     .RequireEnabledAsync("App.ExportPdf", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BeforeAsync_MultipleFeatures_FirstDisabled_ThrowsImmediately()
    {
        IFeatureChecker checker = Substitute.For<IFeatureChecker>();
        checker.RequireEnabledAsync("App.VideoConsultation", Arg.Any<CancellationToken>())
               .ThrowsAsync(new FeatureNotEnabledException("App.VideoConsultation"));
        checker.RequireEnabledAsync("App.ExportPdf", Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);

        Func<Task> act = () => RequiresFeatureMiddleware.BeforeAsync(
            new MultiFeatureMessage(),
            checker,
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<FeatureNotEnabledException>(act);
        await checker.DidNotReceive()
                     .RequireEnabledAsync("App.ExportPdf", Arg.Any<CancellationToken>());
    }
}
