using System.Net;
using Granit.QueryEngine;
using Granit.Workflow.Dtos;
using Granit.Workflow.Endpoints.Extensions;
using Granit.Workflow.Endpoints.Options;
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
/// Verifies that <see cref="WorkflowEndpointRouteBuilderExtensions.MapGranitWorkflow"/>
/// correctly applies route prefix and authorization policy.
/// </summary>
public sealed class WorkflowEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public async Task MapGranitWorkflow_with_custom_prefix_routes_correctly()
    {
        // Arrange
        IWorkflowHistoryQuery historyQuery = Substitute.For<IWorkflowHistoryQuery>();
        historyQuery.GetHistoryAsync("Order", "1", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<WorkflowTransitionHistoryResponse>([], 0, HasMore: false));

        await using WebApplication app = BuildApp(historyQuery, opts =>
        {
            opts.RoutePrefix = "admin/wf";
        });

        HttpClient client = BuildAuthorizedClient(app);

        // Act
        HttpResponseMessage response = await client.GetAsync(
            "/admin/wf/Order/1/history", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapGranitWorkflow_default_prefix_is_workflow()
    {
        // Arrange
        IWorkflowHistoryQuery historyQuery = Substitute.For<IWorkflowHistoryQuery>();
        historyQuery.GetHistoryAsync("Order", "1", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<WorkflowTransitionHistoryResponse>([], 0, HasMore: false));

        await using WebApplication app = BuildApp(historyQuery);
        HttpClient client = BuildAuthorizedClient(app);

        // Act
        HttpResponseMessage response = await client.GetAsync(
            "/workflow/Order/1/history", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapGranitWorkflow_returns_group_builder_for_chaining()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(WorkflowPermissions.History.Read, policy =>
                policy.RequireClaim(TestAuthHandler.PermissionClaimType, WorkflowPermissions.History.Read));
        builder.Services.AddSingleton(Substitute.For<IWorkflowHistoryQuery>());

        WebApplication app = builder.Build();

        // Act
        Microsoft.AspNetCore.Routing.RouteGroupBuilder group = app.MapGranitWorkflow();

        // Assert
        group.ShouldNotBeNull();

        await app.DisposeAsync();
    }

    // -- Helpers ---------------------------------------------------------------

    private static WebApplication BuildApp(
        IWorkflowHistoryQuery historyQuery,
        Action<WorkflowEndpointsOptions>? configure = null)
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
        builder.Services.AddSingleton(historyQuery);

        WebApplication app = builder.Build();
        app.MapGranitWorkflow(configure);
        app.StartAsync().GetAwaiter().GetResult();

        return app;
    }

    private static HttpClient BuildAuthorizedClient(WebApplication app)
    {
        HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, WorkflowPermissions.History.Read);
        return client;
    }
}
