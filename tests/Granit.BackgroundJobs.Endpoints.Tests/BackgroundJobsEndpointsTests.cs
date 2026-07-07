using System.Net;
using System.Net.Http.Json;
using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Endpoints.Extensions;
using Granit.BackgroundJobs.Endpoints.Permissions;
using Granit.Exceptions;
using Granit.QueryEngine;
using Granit.Testing.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Endpoints.Tests;

/// <summary>
/// HTTP-level tests for all background jobs administration endpoints, hosted by
/// <see cref="GranitEndpointTestHost"/> (canonical endpoint harness, #2132).
/// Covers the least-privilege matrix: read endpoints require only
/// <c>BackgroundJobs.Jobs.Read</c>, write endpoints only <c>BackgroundJobs.Jobs.Manage</c> —
/// never both.
/// </summary>
public sealed class BackgroundJobsEndpointsTests : IAsyncLifetime
{
    private const string Prefix = "/background-jobs/jobs";

    // A permission the endpoints never require, so its holder authenticates yet stays forbidden.
    private const string UnrelatedPermission = "BackgroundJobs.Jobs.Unrelated";

    private readonly IBackgroundJobReader _reader = Substitute.For<IBackgroundJobReader>();
    private readonly IBackgroundJobWriter _writer = Substitute.For<IBackgroundJobWriter>();

    private GranitEndpointTestHost _host = null!;
    private HttpClient _adminClient = null!;
    private HttpClient _readOnlyClient = null!;
    private HttpClient _manageOnlyClient = null!;
    private HttpClient _unrelatedClient = null!;
    private HttpClient _anonClient = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await GranitEndpointTestHost.StartAsync(
            services =>
            {
                services.AddAuthorizationBuilder()
                    .AddPolicy(
                        BackgroundJobsPermissions.Jobs.Read,
                        policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, BackgroundJobsPermissions.Jobs.Read))
                    .AddPolicy(
                        BackgroundJobsPermissions.Jobs.Manage,
                        policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, BackgroundJobsPermissions.Jobs.Manage));
                services.AddSingleton(_reader);
                services.AddSingleton(_writer);
            },
            app => app.MapGranitBackgroundJobs());

        _adminClient = _host.CreateClientWithPermissions(
            BackgroundJobsPermissions.Jobs.Read, BackgroundJobsPermissions.Jobs.Manage);
        _readOnlyClient = _host.CreateClientWithPermissions(BackgroundJobsPermissions.Jobs.Read);
        _manageOnlyClient = _host.CreateClientWithPermissions(BackgroundJobsPermissions.Jobs.Manage);
        _unrelatedClient = _host.CreateClientWithPermissions(UnrelatedPermission);
        _anonClient = _host.CreateAnonymousClient();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    // ── GET / ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithAdminToken_Returns200WithList()
    {
        // Arrange
        IReadOnlyList<BackgroundJobStatus> jobs =
        [
            BuildStatus("daily-report", isEnabled: true),
            BuildStatus("monthly-export", isEnabled: true),
            BuildStatus("weekly-cleanup", isEnabled: false),
        ];
        _reader.GetAllAsync(Arg.Any<CancellationToken>()).Returns(jobs);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(Prefix, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<BackgroundJobStatus>? result =
            await response.Content.ReadFromJsonAsync<PagedResult<BackgroundJobStatus>>(
                TestContext.Current.CancellationToken);
        result!.Items.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetAll_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(Prefix, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithoutRequiredPermission_Returns403()
    {
        HttpResponseMessage response = await _unrelatedClient.GetAsync(Prefix, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Least-privilege matrix ────────────────────────────────────────────────
    // Read endpoints require ONLY the Read permission; write endpoints ONLY Manage.
    // Regression guard: chaining both RequireAuthorization calls on a single group
    // used to demand Read AND Manage on every endpoint.

    [Fact]
    public async Task GetAll_WithReadPermissionOnly_Returns200()
    {
        _reader.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<BackgroundJobStatus>)[]);

        HttpResponseMessage response = await _readOnlyClient.GetAsync(Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByName_WithReadPermissionOnly_Returns200()
    {
        _reader.FindAsync("daily-report", Arg.Any<CancellationToken>())
            .Returns(BuildStatus("daily-report", isEnabled: true));

        HttpResponseMessage response = await _readOnlyClient.GetAsync(
            $"{Prefix}/daily-report", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Pause_WithReadPermissionOnly_Returns403()
    {
        HttpResponseMessage response = await _readOnlyClient.PostAsync(
            $"{Prefix}/daily-report/pause", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await _writer.DidNotReceive().PauseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Trigger_WithReadPermissionOnly_Returns403()
    {
        HttpResponseMessage response = await _readOnlyClient.PostAsync(
            $"{Prefix}/daily-report/trigger", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pause_WithManagePermissionOnly_Returns204()
    {
        _writer.PauseAsync("daily-report", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        HttpResponseMessage response = await _manageOnlyClient.PostAsync(
            $"{Prefix}/daily-report/pause", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetAll_WithManagePermissionOnly_Returns403()
    {
        HttpResponseMessage response = await _manageOnlyClient.GetAsync(Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── GET /{name} ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByName_WhenJobExists_Returns200()
    {
        // Arrange
        BackgroundJobStatus job = BuildStatus("daily-report", isEnabled: true);
        _reader.FindAsync("daily-report", Arg.Any<CancellationToken>()).Returns(job);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/daily-report", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        BackgroundJobStatus? result =
            await response.Content.ReadFromJsonAsync<BackgroundJobStatus>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.JobName.ShouldBe("daily-report");
    }

    [Fact]
    public async Task GetByName_WhenJobNotFound_Returns404()
    {
        // Arrange
        _reader.FindAsync("ghost-job", Arg.Any<CancellationToken>()).Returns((BackgroundJobStatus?)null);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/ghost-job", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── POST /{name}/pause ────────────────────────────────────────────────────

    [Fact]
    public async Task Pause_WhenJobExists_Returns204()
    {
        // Arrange
        _writer.PauseAsync("daily-report", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/daily-report/pause", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _writer.Received(1).PauseAsync("daily-report", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pause_WhenJobNotFound_Returns404()
    {
        // Arrange
        _writer.PauseAsync("ghost-job", Arg.Any<CancellationToken>())
            .ThrowsAsync(new EntityNotFoundException(typeof(BackgroundJobDefinition), "ghost-job"));

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/ghost-job/pause", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── POST /{name}/resume ───────────────────────────────────────────────────

    [Fact]
    public async Task Resume_WhenJobExists_Returns204()
    {
        // Arrange
        _writer.ResumeAsync("daily-report", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/daily-report/resume", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _writer.Received(1).ResumeAsync("daily-report", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resume_WhenJobNotFound_Returns404()
    {
        // Arrange
        _writer.ResumeAsync("ghost-job", Arg.Any<CancellationToken>())
            .ThrowsAsync(new EntityNotFoundException(typeof(BackgroundJobDefinition), "ghost-job"));

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/ghost-job/resume", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── POST /{name}/trigger ──────────────────────────────────────────────────

    [Fact]
    public async Task Trigger_WhenJobExists_Returns202()
    {
        // Arrange — TriggerNowAsync is fire-and-forget (async enqueue)
        _writer.TriggerNowAsync("monthly-export", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/monthly-export/trigger", content: null, TestContext.Current.CancellationToken);

        // Assert — 202 Accepted, not 200 (processing is async via Wolverine)
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await _writer.Received(1).TriggerNowAsync("monthly-export", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Trigger_WhenJobNotFound_Returns404()
    {
        // Arrange
        _writer.TriggerNowAsync("ghost-job", Arg.Any<CancellationToken>())
            .ThrowsAsync(new EntityNotFoundException(typeof(BackgroundJobDefinition), "ghost-job"));

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/ghost-job/trigger", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BackgroundJobStatus BuildStatus(string name, bool isEnabled) =>
        new(
            JobName: name,
            CronExpression: "0 8 * * *",
            IsEnabled: isEnabled,
            LastExecutedAt: null,
            NextExecutionAt: null,
            ConsecutiveFailures: 0,
            DeadLetterCount: 0,
            LastError: null);
}
