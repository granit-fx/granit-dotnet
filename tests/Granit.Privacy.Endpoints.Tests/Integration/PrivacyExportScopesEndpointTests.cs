using System.Net;
using System.Net.Http.Json;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Endpoints.Dtos;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Integration;

public sealed class PrivacyExportScopesEndpointTests
{
    [Fact]
    public async Task GetScopes_ReturnsResolverDescriptorsAsPrivacyExportScopeResponses()
    {
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();
        server.ScopeResolver.ListVisibleAsync(Arg.Any<PrivacyExportContext>(), Arg.Any<CancellationToken>())
            .Returns([
                new ProviderDescriptor("identity-local", "Privacy.Scopes.IdentityLocal", null),
                new ProviderDescriptor("documents", "Privacy.Scopes.Documents", "Documents.Privacy", DefaultSelected: false, EstimatedSizeBytes: 12_345),
            ]);

        HttpResponseMessage response = await server.AuthenticatedClient.GetAsync(
            "/privacy/exports/scopes", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<PrivacyExportScopeResponse>? body = await response.Content.ReadFromJsonAsync<IReadOnlyList<PrivacyExportScopeResponse>>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body!.Count.ShouldBe(2);
        body[0].ProviderName.ShouldBe("identity-local");
        body[0].DisplayKey.ShouldBe("Privacy.Scopes.IdentityLocal");
        body[0].DefaultSelected.ShouldBeTrue();
        body[0].EstimatedSizeBytes.ShouldBeNull();
        body[1].ProviderName.ShouldBe("documents");
        body[1].FeatureName.ShouldBe("Documents.Privacy");
        body[1].DefaultSelected.ShouldBeFalse();
        body[1].EstimatedSizeBytes.ShouldBe(12_345);
    }

    [Fact]
    public async Task GetScopes_AnonymousClient_Returns401()
    {
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();

        HttpResponseMessage response = await server.AnonymousClient.GetAsync(
            "/privacy/exports/scopes", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostExports_WithoutScopes_PublishesEtoWithNullRequestedScopes()
    {
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();

        HttpResponseMessage response = await server.AuthenticatedClient.PostAsync(
            "/privacy/exports", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await server.EventBus.Received(1).PublishAsync(
            Arg.Is<PersonalDataRequestedEto>(e => e.RequestedScopes == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostExports_WithScopes_PublishesEtoCarryingTheScopes()
    {
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();

        HttpResponseMessage response = await server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/exports",
            new PrivacyExportRequest(Scopes: ["identity-local", "documents"]),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await server.EventBus.Received(1).PublishAsync(
            Arg.Is<PersonalDataRequestedEto>(e =>
                e.RequestedScopes != null &&
                e.RequestedScopes.Count == 2 &&
                e.RequestedScopes.Contains("identity-local") &&
                e.RequestedScopes.Contains("documents")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostExports_WithEmptyScopesArray_TreatedAsAllVisible()
    {
        await using PrivacyEndpointsTestServer server = await PrivacyEndpointsTestServer.CreateAsync();

        HttpResponseMessage response = await server.AuthenticatedClient.PostAsJsonAsync(
            "/privacy/exports",
            new PrivacyExportRequest(Scopes: []),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        // Empty array is normalised to null (Takeout default: "everything visible").
        await server.EventBus.Received(1).PublishAsync(
            Arg.Is<PersonalDataRequestedEto>(e => e.RequestedScopes == null),
            Arg.Any<CancellationToken>());
    }
}
