using System.Net;
using System.Net.Http.Json;
using Granit.Core.MultiTenancy;
using Granit.Features.Definitions;
using Granit.Features.Endpoints.Dtos;
using Granit.Features.Endpoints.Extensions;
using Granit.Features.ValueTypes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Features.Endpoints.Tests;

public sealed class FeaturesEndpointTests : IAsyncDisposable
{
    private const string AuthRole = "authenticated";
    private const string Prefix = "/features";

    private readonly IFeatureDefinitionStore _definitionStore = Substitute.For<IFeatureDefinitionStore>();
    private readonly IFeatureChecker _featureChecker = Substitute.For<IFeatureChecker>();
    private readonly IFeatureStoreWriter _storeWriter = Substitute.For<IFeatureStoreWriter>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    private static readonly FeatureDefinition ToggleFeature = new("Acme.VideoConference", "false", FeatureValueType.Toggle)
    {
        DisplayName = "Video Conference",
        Description = "Enables video conferencing for the tenant.",
    };

    private static readonly FeatureDefinition NumericFeature = new("Acme.MaxUsers", "50", FeatureValueType.Numeric)
    {
        NumericConstraint = new NumericConstraint(1, 10_000),
        DisplayName = "Max Users",
    };

    private static readonly FeatureDefinition SelectionFeature = new("Acme.Theme", "light", FeatureValueType.Selection)
    {
        SelectionValues = new SelectionValues(["light", "dark", "auto"]),
        DisplayName = "Theme",
    };

    public FeaturesEndpointTests()
    {
        _definitionStore.GetAll().Returns([ToggleFeature, NumericFeature, SelectionFeature]);
        _definitionStore.GetOrNull("Acme.VideoConference").Returns(ToggleFeature);
        _definitionStore.GetOrNull("Acme.MaxUsers").Returns(NumericFeature);
        _definitionStore.GetOrNull("Acme.Theme").Returns(SelectionFeature);
        _definitionStore.GetOrNull("Unknown.Feature").Returns((FeatureDefinition?)null);

        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("Features.Flags.Read", p => p.RequireAuthenticatedUser())
            .AddPolicy("Features.Flags.Manage", p => p.RequireAuthenticatedUser());

        builder.Services.AddSingleton(_definitionStore);
        builder.Services.AddSingleton(_featureChecker);
        builder.Services.AddSingleton(_storeWriter);
        builder.Services.AddSingleton(_currentTenant);
        builder.Services.AddSingleton<IFeatureDefinitionProvider, TestFeatureDefinitionProvider>();

        _app = builder.Build();
        _app.MapGranitFeatures();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient(AuthRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _authClient.Dispose();
        _anonClient.Dispose();
        await _app.DisposeAsync();
    }

    // =========================================================================
    // GET /features/definitions
    // =========================================================================

    [Fact]
    public async Task GetDefinitions_Authenticated_Returns_Grouped_Definitions()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/definitions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<FeatureGroupResponse>? groups = await response.Content
            .ReadFromJsonAsync<List<FeatureGroupResponse>>(TestContext.Current.CancellationToken);

        groups.ShouldNotBeNull();
        groups.Count.ShouldBe(1);
        groups[0].Name.ShouldBe("Acme");
        groups[0].Features.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetDefinitions_Returns_Correct_ValueType_And_Constraints()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/definitions", TestContext.Current.CancellationToken);

        List<FeatureGroupResponse>? groups = await response.Content
            .ReadFromJsonAsync<List<FeatureGroupResponse>>(TestContext.Current.CancellationToken);

        FeatureDefinitionResponse? numeric = groups![0].Features
            .FirstOrDefault(f => f.Name == "Acme.MaxUsers");
        numeric.ShouldNotBeNull();
        numeric.ValueType.ShouldBe("Numeric");
        numeric.NumericConstraint.ShouldNotBeNull();
        numeric.NumericConstraint.Min.ShouldBe(1);
        numeric.NumericConstraint.Max.ShouldBe(10_000);

        FeatureDefinitionResponse? selection = groups[0].Features
            .FirstOrDefault(f => f.Name == "Acme.Theme");
        selection.ShouldNotBeNull();
        selection.ValueType.ShouldBe("Selection");
        selection.SelectionValues.ShouldNotBeNull();
        selection.SelectionValues.ShouldContain("dark");
    }

    [Fact]
    public async Task GetDefinitions_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/definitions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // GET /features/values
    // =========================================================================

    [Fact]
    public async Task GetAllValues_Authenticated_Returns_All_Resolved_Values()
    {
        _featureChecker.GetValueAsync("Acme.VideoConference", Arg.Any<CancellationToken>())
            .Returns("true");
        _featureChecker.GetValueAsync("Acme.MaxUsers", Arg.Any<CancellationToken>())
            .Returns("100");
        _featureChecker.GetValueAsync("Acme.Theme", Arg.Any<CancellationToken>())
            .Returns("dark");

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/values", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Dictionary<string, string>? result = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result["Acme.VideoConference"].ShouldBe("true");
        result["Acme.MaxUsers"].ShouldBe("100");
        result["Acme.Theme"].ShouldBe("dark");
    }

    [Fact]
    public async Task GetAllValues_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/values", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // GET /features/values/{name}
    // =========================================================================

    [Fact]
    public async Task GetValue_Known_Feature_Returns_Value()
    {
        _featureChecker.GetValueAsync("Acme.VideoConference", Arg.Any<CancellationToken>())
            .Returns("true");

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/values/Acme.VideoConference", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        FeatureValueResponse? result = await response.Content
            .ReadFromJsonAsync<FeatureValueResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("Acme.VideoConference");
        result.Value.ShouldBe("true");
    }

    [Fact]
    public async Task GetValue_Unknown_Feature_Returns_404()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/values/Unknown.Feature", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // PUT /features/overrides/{name}
    // =========================================================================

    [Fact]
    public async Task SetOverride_Toggle_Valid_Returns_204()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/overrides/Acme.VideoConference",
            new SetFeatureOverrideRequest("true"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _storeWriter.Received(1).SetAsync(
            "Acme.VideoConference",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "true",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverride_Toggle_Invalid_Returns_422()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/overrides/Acme.VideoConference",
            new SetFeatureOverrideRequest("maybe"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SetOverride_Numeric_Valid_Returns_204()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/overrides/Acme.MaxUsers",
            new SetFeatureOverrideRequest("500"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _storeWriter.Received(1).SetAsync(
            "Acme.MaxUsers",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "500",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverride_Numeric_OutOfRange_Returns_422()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/overrides/Acme.MaxUsers",
            new SetFeatureOverrideRequest("99999"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SetOverride_Numeric_NotANumber_Returns_422()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/overrides/Acme.MaxUsers",
            new SetFeatureOverrideRequest("abc"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SetOverride_Selection_Valid_Returns_204()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/overrides/Acme.Theme",
            new SetFeatureOverrideRequest("dark"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _storeWriter.Received(1).SetAsync(
            "Acme.Theme",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "dark",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverride_Selection_Invalid_Returns_422()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/overrides/Acme.Theme",
            new SetFeatureOverrideRequest("neon"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SetOverride_Unknown_Feature_Returns_404()
    {
        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/overrides/Unknown.Feature",
            new SetFeatureOverrideRequest("true"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetOverride_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.PutAsJsonAsync(
            $"{Prefix}/overrides/Acme.VideoConference",
            new SetFeatureOverrideRequest("true"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // DELETE /features/overrides/{name}
    // =========================================================================

    [Fact]
    public async Task DeleteOverride_Known_Feature_Returns_204()
    {
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/overrides/Acme.VideoConference",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _storeWriter.Received(1).DeleteAsync(
            "Acme.VideoConference",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteOverride_Unknown_Feature_Returns_404()
    {
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/overrides/Unknown.Feature",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteOverride_Anonymous_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.DeleteAsync(
            $"{Prefix}/overrides/Acme.VideoConference",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
