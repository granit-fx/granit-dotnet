using System.Net;
using System.Net.Http.Json;
using Granit.QueryEngine;
using Granit.Workflow.Dtos;
using Granit.Workflow.Endpoints.Extensions;
using Granit.Workflow.Endpoints.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Endpoints.Tests;

/// <summary>
/// Integration tests for the workflow read endpoints (GET /history).
/// Uses a real <see cref="WebApplication"/> with <see cref="TestServer"/>.
/// </summary>
public sealed class WorkflowReadEndpointsTests : IAsyncDisposable
{
    private const string WorkflowPrefix = "/workflow";

    private readonly IWorkflowHistoryQuery _historyQuery = Substitute.For<IWorkflowHistoryQuery>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public WorkflowReadEndpointsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(WorkflowPermissions.History.Read, policy =>
                policy.RequireClaim(TestAuthHandler.PermissionClaimType, WorkflowPermissions.History.Read));
        builder.Services.AddSingleton(_historyQuery);

        _app = builder.Build();
        _app.MapGranitWorkflow();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(WorkflowPermissions.History.Read);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- GET /{entityType}/{entityId}/history ---------------------------------

    [Fact]
    public async Task GetHistory_returns_paginated_history_entries()
    {
        // Arrange
        _historyQuery.GetHistoryAsync("Order", "42", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<WorkflowTransitionHistoryResponse>(
            [
                new("Draft", "Submitted", DateTimeOffset.UtcNow.AddHours(-2), "user-1", null),
                new("Submitted", "Approved", DateTimeOffset.UtcNow.AddHours(-1), "user-2", "LGTM"),
            ], 2, HasMore: false));

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{WorkflowPrefix}/Order/42/history", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<WorkflowTransitionHistoryResponse>? result = await response.Content
            .ReadFromJsonAsync<PagedResult<WorkflowTransitionHistoryResponse>>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.Items[0].PreviousState.ShouldBe("Draft");
        result.Items[0].NewState.ShouldBe("Submitted");
        result.Items[1].NewState.ShouldBe("Approved");
        result.Items[1].Comment.ShouldBe("LGTM");
    }

    [Fact]
    public async Task GetHistory_empty_returns_empty_paged_result()
    {
        // Arrange
        _historyQuery.GetHistoryAsync("Order", "99", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<WorkflowTransitionHistoryResponse>([], 0, HasMore: false));

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{WorkflowPrefix}/Order/99/history", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<WorkflowTransitionHistoryResponse>? result = await response.Content
            .ReadFromJsonAsync<PagedResult<WorkflowTransitionHistoryResponse>>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetHistory_without_auth_returns_401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{WorkflowPrefix}/Order/42/history", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHistory_without_permission_returns_403()
    {
        // Arrange — authenticated but lacking the required permission.
        HttpClient unauthorizedClient = BuildClient("some.other.permission");

        // Act
        HttpResponseMessage response = await unauthorizedClient.GetAsync(
            $"{WorkflowPrefix}/Order/42/history", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetHistory_preserves_comment_null()
    {
        // Arrange
        _historyQuery.GetHistoryAsync("Invoice", "7", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<WorkflowTransitionHistoryResponse>(
            [
                new("Draft", "Sent", DateTimeOffset.UtcNow, "user-1", null),
            ], 1, HasMore: false));

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{WorkflowPrefix}/Invoice/7/history", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<WorkflowTransitionHistoryResponse>? result = await response.Content
            .ReadFromJsonAsync<PagedResult<WorkflowTransitionHistoryResponse>>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Items[0].Comment.ShouldBeNull();
    }

    // -- Helpers ---------------------------------------------------------------

    private HttpClient BuildClient(string permission)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, permission);
        return client;
    }
}
