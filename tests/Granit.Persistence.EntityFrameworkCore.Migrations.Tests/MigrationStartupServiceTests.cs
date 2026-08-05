// =============================================================================
// Tests — MigrationStartupService
// =============================================================================
// The service is a thin startup trigger since the orchestration unification
// (#3167): it delegates to IGranitMigrationRunner in ResumeBatches mode — the
// runner owns the lock, timeout, and exit-code semantics. These tests verify
// the delegation, the missing-runner warning path, and that neither failures
// nor exceptions block application startup.
// =============================================================================

using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationStartupServiceTests
{
    [Fact]
    public async Task StartAsync_DelegatesToRunner_InResumeBatchesMode()
    {
        IGranitMigrationRunner runner = Substitute.For<IGranitMigrationRunner>();
        runner.RunAsync(Arg.Any<MigrationRunMode>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        MigrationStartupService sut = new(NullLogger<MigrationStartupService>.Instance, runner);

        await sut.StartAsync(TestContext.Current.CancellationToken);

        await runner.Received(1).RunAsync(
            MigrationRunMode.ResumeBatches, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_RunnerNotRegistered_DoesNotThrow()
    {
        // Migrations-only host (AddGranitMigrateSupport never called): the service warns
        // and skips instead of maintaining a parallel resume pipeline.
        MigrationStartupService sut = new(
            NullLogger<MigrationStartupService>.Instance, migrationRunner: null);

        await Should.NotThrowAsync(
            () => sut.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_NonZeroExitCode_DoesNotThrow()
    {
        IGranitMigrationRunner runner = Substitute.For<IGranitMigrationRunner>();
        runner.RunAsync(Arg.Any<MigrationRunMode>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        MigrationStartupService sut = new(NullLogger<MigrationStartupService>.Instance, runner);

        // A failed resume is logged, never rethrown — startup must continue.
        await Should.NotThrowAsync(
            () => sut.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_RunnerThrows_DoesNotPropagateException()
    {
        IGranitMigrationRunner runner = Substitute.For<IGranitMigrationRunner>();
        runner.RunAsync(Arg.Any<MigrationRunMode>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("db unavailable"));

        MigrationStartupService sut = new(NullLogger<MigrationStartupService>.Instance, runner);

        await Should.NotThrowAsync(
            () => sut.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StopAsync_CompletesSynchronously()
    {
        MigrationStartupService sut = new(
            NullLogger<MigrationStartupService>.Instance, migrationRunner: null);

        Task stop = sut.StopAsync(TestContext.Current.CancellationToken);

        stop.IsCompletedSuccessfully.ShouldBeTrue();
        await stop;
    }
}
