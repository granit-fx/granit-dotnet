using Granit.Persistence.EntityFrameworkCore.Hosting.Extensions;
using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Tests;

/// <summary>
/// Tests for <see cref="PersistenceHostingWebApplicationExtensions"/> — covers the migrate-flag
/// probe and the WebApplication-level migration runner wrapper (exit-code passthrough,
/// unhandled-exception path, and external-cancellation rethrow).
/// </summary>
public sealed class PersistenceHostingWebApplicationExtensionsTests
{
    // -------------------------------------------------------------------------
    // HasGranitMigrateFlag
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HasGranitMigrateFlag_ReturnsFalse_WhenOptionsNotRegistered()
    {
        await using WebApplication app = WebApplication.CreateBuilder().Build();

        app.HasGranitMigrateFlag().ShouldBeFalse();
    }

    [Fact]
    public async Task HasGranitMigrateFlag_ReturnsFalse_WhenCliFlagIsNotInProcessArgs()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(new GranitMigrateOptions
        {
            // A flag value that is virtually guaranteed not to appear in the test runner's
            // argv. The probe scans Environment.GetCommandLineArgs(), so this asserts the
            // "options registered + flag absent" branch.
            CliFlag = "--granit-flag-absent-from-test-runner-args-zzz",
        });
        await using WebApplication app = builder.Build();

        app.HasGranitMigrateFlag().ShouldBeFalse();
    }

    [Fact]
    public async Task HasGranitMigrateFlag_ReturnsTrue_WhenCliFlagMatchesAProcessArg()
    {
        // Pin the CliFlag to a value we know is present in Environment.GetCommandLineArgs()
        // for this test run — the test host executable path is always argv[0] and never
        // empty, so we use it as a stable, runner-agnostic match.
        string processArgZero = Environment.GetCommandLineArgs()[0];

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(new GranitMigrateOptions { CliFlag = processArgZero });
        await using WebApplication app = builder.Build();

        app.HasGranitMigrateFlag().ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // RunGranitMigrationsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunGranitMigrationsAsync_ReturnsRunnerExitCode_OnSuccess()
    {
        IGranitMigrationRunner runner = Substitute.For<IGranitMigrationRunner>();
        runner.RunAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(runner);
        WebApplication app = builder.Build();

        int exitCode = await app.RunGranitMigrationsAsync();

        exitCode.ShouldBe(0);
        await runner.Received(1).RunAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunGranitMigrationsAsync_PropagatesNonZeroExitCode()
    {
        IGranitMigrationRunner runner = Substitute.For<IGranitMigrationRunner>();
        runner.RunAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(2));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(runner);
        WebApplication app = builder.Build();

        int exitCode = await app.RunGranitMigrationsAsync();

        exitCode.ShouldBe(2);
    }

    [Fact]
    public async Task RunGranitMigrationsAsync_Returns1_WhenRunnerThrowsUnhandledException()
    {
        IGranitMigrationRunner runner = Substitute.For<IGranitMigrationRunner>();
        runner.RunAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("kaboom"));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(runner);
        WebApplication app = builder.Build();

        // The default exitCode before the try is 1; the catch logs and falls through to
        // the finally without resetting the code — so unhandled exceptions surface as 1.
        int exitCode = await app.RunGranitMigrationsAsync();

        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task RunGranitMigrationsAsync_RethrowsOperationCanceledException()
    {
        IGranitMigrationRunner runner = Substitute.For<IGranitMigrationRunner>();
        runner.RunAsync(Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException("external cancel"));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(runner);
        WebApplication app = builder.Build();

        await Should.ThrowAsync<OperationCanceledException>(() => app.RunGranitMigrationsAsync());
    }
}
