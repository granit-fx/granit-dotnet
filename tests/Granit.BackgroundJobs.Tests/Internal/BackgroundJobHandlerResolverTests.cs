using Granit.BackgroundJobs.Internal;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Internal;

/// <summary>
/// Covers the Wolverine-equivalent binding rule: the handler is found by the type of the
/// handle method's first parameter, not by the handler class name (#3206).
/// </summary>
public sealed class BackgroundJobHandlerResolverTests
{
    [Fact]
    public void Resolve_HandlerNamedAfterTheAction_BindsTheJob()
    {
        // The shape every Granit.{Module}.BackgroundJobs satellite ships: the job is
        // {Action}Job, the handler {Action}Handler — no "Job" in the handler name.
        BackgroundJobHandlerResolver.HandlerBinding binding =
            BackgroundJobHandlerResolver.Resolve(typeof(SatelliteShapedJob));

        binding.HandlerType.ShouldBe(typeof(SatelliteShapedHandler));
        binding.Method.Name.ShouldBe("HandleAsync");
    }

    [Fact]
    public void Resolve_HandlerNamedAfterTheJob_BindsTheJob()
    {
        BackgroundJobHandlerResolver.HandlerBinding binding =
            BackgroundJobHandlerResolver.Resolve(typeof(ExactlyNamedJob));

        binding.HandlerType.ShouldBe(typeof(ExactlyNamedJobHandler));
    }

    [Fact]
    public void Resolve_ExactlyNamedHandlerWins_OverAnAssignableFallback()
    {
        // PreferredJobHandler takes the job exactly; BaseJobConsumer takes the base type
        // and would otherwise be a candidate too.
        BackgroundJobHandlerResolver.HandlerBinding binding =
            BackgroundJobHandlerResolver.Resolve(typeof(PreferredJob));

        binding.HandlerType.ShouldBe(typeof(PreferredJobHandler));
    }

    [Fact]
    public void Resolve_ConsumerSuffixAndConsumeMethod_BindsTheJob()
    {
        BackgroundJobHandlerResolver.HandlerBinding binding =
            BackgroundJobHandlerResolver.Resolve(typeof(ConsumedJob));

        binding.HandlerType.ShouldBe(typeof(ConsumedJobConsumer));
        binding.Method.Name.ShouldBe("Consume");
    }

    [Fact]
    public void Resolve_StaticHandlerClass_BindsTheJob()
    {
        BackgroundJobHandlerResolver.HandlerBinding binding =
            BackgroundJobHandlerResolver.Resolve(typeof(StaticallyHandledJob));

        binding.HandlerType.ShouldBe(typeof(StaticallyHandledHandler));
    }

    [Fact]
    public void Resolve_NoHandlerInAssembly_Throws()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => BackgroundJobHandlerResolver.Resolve(typeof(UnhandledJob)));

        ex.Message.ShouldContain(nameof(UnhandledJob));
    }

    [Fact]
    public void Resolve_TypeWithoutHandlerSuffix_IsNotACandidate()
    {
        // NotAHandlerNamedType has the right method shape but the wrong class name —
        // Wolverine would not discover it either.
        Should.Throw<InvalidOperationException>(
            () => BackgroundJobHandlerResolver.Resolve(typeof(WronglyNamedHandlerJob)));
    }

    [Fact]
    public void Resolve_TwoEquallyGoodHandlers_ThrowsRatherThanPickingOne()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => BackgroundJobHandlerResolver.Resolve(typeof(AmbiguousJob)));

        ex.Message.ShouldContain("Ambiguous");
        ex.Message.ShouldContain(nameof(FirstAmbiguousHandler));
        ex.Message.ShouldContain(nameof(SecondAmbiguousHandler));
    }

    [Fact]
    public void Resolve_CalledTwice_ReturnsTheSameBinding()
    {
        BackgroundJobHandlerResolver.HandlerBinding first =
            BackgroundJobHandlerResolver.Resolve(typeof(SatelliteShapedJob));
        BackgroundJobHandlerResolver.HandlerBinding second =
            BackgroundJobHandlerResolver.Resolve(typeof(SatelliteShapedJob));

        second.ShouldBe(first);
    }

    // =========================================================================
    // Test doubles
    // =========================================================================

    public sealed record SatelliteShapedJob : IBackgroundJob;

    public sealed class SatelliteShapedHandler
    {
        public static Task HandleAsync(SatelliteShapedJob job, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public sealed record ExactlyNamedJob : IBackgroundJob;

    public sealed class ExactlyNamedJobHandler
    {
        public static Task HandleAsync(ExactlyNamedJob job, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public abstract record BaseJob : IBackgroundJob;

    public sealed record PreferredJob : BaseJob;

    public sealed class PreferredJobHandler
    {
        public static Task HandleAsync(PreferredJob job, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public sealed class BaseJobConsumer
    {
        public static Task ConsumeAsync(BaseJob job, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public sealed record ConsumedJob : IBackgroundJob;

    public sealed class ConsumedJobConsumer
    {
        public static void Consume(ConsumedJob job)
        {
            // No-op: the resolver only needs the binding, not a side effect.
        }
    }

    public sealed record StaticallyHandledJob : IBackgroundJob;

    public static class StaticallyHandledHandler
    {
        public static Task HandleAsync(StaticallyHandledJob job, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public sealed record UnhandledJob : IBackgroundJob;

    public sealed record WronglyNamedHandlerJob : IBackgroundJob;

    public sealed class WronglyNamedHandlerListener
    {
        public static Task HandleAsync(WronglyNamedHandlerJob job, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public sealed record AmbiguousJob : IBackgroundJob;

    public sealed class FirstAmbiguousHandler
    {
        public static Task HandleAsync(AmbiguousJob job, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    public sealed class SecondAmbiguousHandler
    {
        public static Task HandleAsync(AmbiguousJob job, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
