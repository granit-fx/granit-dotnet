using System.Diagnostics;
using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Identity;
using Granit.Identity.Diagnostics;
using Granit.Identity.Federated.Keycloak.Exceptions;
using Granit.Identity.Federated.Keycloak.Internal;
using Granit.Identity.Federated.RateLimiting;
using Granit.Identity.Models;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests;

/// <summary>
/// Covers Phase 2 <see cref="IIdentityClientRoleManager"/> methods on
/// <see cref="KeycloakIdentityProvider"/> — mock-based (same pattern as
/// <see cref="KeycloakIdentityProviderTests"/>).
/// </summary>
public sealed class KeycloakClientRoleTests : IDisposable
{
    private readonly Granit.Identity.Federated.Keycloak.Options.KeycloakAdminOptions _options = new()
    {
        BaseUrl = "https://keycloak.test",
        Realm = "test-realm",
        ClientId = "admin-service",
        ClientSecret = "secret",
    };

    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly ServiceProvider _metricsServiceProvider;
    private readonly IdentityMetrics _metrics;
    private readonly ActivityListener _activityListener;

    public KeycloakClientRoleTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Granit.Identity.Keycloak",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);

        ServiceCollection svc = [];
        svc.AddMetrics();
        _metricsServiceProvider = svc.BuildServiceProvider();
        _metrics = new IdentityMetrics(_metricsServiceProvider.GetRequiredService<IMeterFactory>());
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _metricsServiceProvider.Dispose();
    }

    private KeycloakIdentityProvider BuildProvider(params string[] responses)
    {
        MockSequenceHttpMessageHandler handler = new(responses);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://keycloak.test/") };

        MockHttpMessageHandler tokenHandler = new()
        {
            ResponseBody = """{"access_token":"admin-token","expires_in":300}""",
        };
        HttpClient tokenClient = new(tokenHandler) { BaseAddress = new Uri("https://keycloak.test/") };

        // Token acquisition uses "KeycloakAdmin" client, admin calls use "KeycloakAdmin"
        // client too — here the same client name serves both in sequence: the MockSequence
        // advances per-call so tests control both token + data responses by ordering.
        // To keep isolation, use separate factories.
        IHttpClientFactory tokenFactory = Substitute.For<IHttpClientFactory>();
        tokenFactory.CreateClient("KeycloakAdmin").Returns(tokenClient);
        IHttpClientFactory adminFactory = _httpClientFactory;
        adminFactory.CreateClient("KeycloakAdmin").Returns(httpClient);

        Microsoft.Extensions.Options.IOptions<Granit.Identity.Federated.Keycloak.Options.KeycloakAdminOptions> opts =
            Microsoft.Extensions.Options.Options.Create(_options);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        KeycloakAdminTokenService tokenService = new(
            tokenFactory, opts, clock, NullLogger<KeycloakAdminTokenService>.Instance);
        ITokenExchangeRateLimiter rateLimiter = Substitute.For<ITokenExchangeRateLimiter>();
        rateLimiter.CheckAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TokenExchangeRateLimitDecision.Allowed);
        KeycloakUserTokenExchangeService tokenExchangeService = new(
            tokenFactory, opts, rateLimiter, _eventBus, TimeProvider.System,
            NullLogger<KeycloakUserTokenExchangeService>.Instance);

        return new KeycloakIdentityProvider(
            tokenService, tokenExchangeService, adminFactory,
            opts, _eventBus, _metrics, NullLogger<KeycloakIdentityProvider>.Instance);
    }

    [Fact]
    public async Task GetClientsAsync_ReturnsClientIds_FromClientsEndpoint()
    {
        const string response = """
            [
              {"id":"uuid-1","clientId":"app-a"},
              {"id":"uuid-2","clientId":"app-b"}
            ]
            """;
        KeycloakIdentityProvider provider = BuildProvider(response);

        IReadOnlyList<string> clients = await provider.GetClientsAsync(TestContext.Current.CancellationToken);

        clients.ShouldBe(["app-a", "app-b"]);
    }

    [Fact]
    public async Task GetClientRolesAsync_ResolvesUuid_ThenListsRoles_PopulatesClientId()
    {
        const string resolveResponse = """[{"id":"client-uuid","clientId":"app-a"}]""";
        const string rolesResponse = """
            [
              {"id":"r1","name":"editor","description":"edit"},
              {"id":"r2","name":"viewer","description":null}
            ]
            """;
        KeycloakIdentityProvider provider = BuildProvider(resolveResponse, rolesResponse);

        IReadOnlyList<IdentityRole> roles = await provider.GetClientRolesAsync(
            "app-a", TestContext.Current.CancellationToken);

        roles.Count.ShouldBe(2);
        roles[0].Name.ShouldBe("editor");
        roles[0].ClientId.ShouldBe("app-a");
        roles[1].Name.ShouldBe("viewer");
        roles[1].ClientId.ShouldBe("app-a");
    }

    [Fact]
    public async Task GetClientRolesAsync_UnknownClientId_Throws()
    {
        const string emptyResolve = "[]";
        KeycloakIdentityProvider provider = BuildProvider(emptyResolve);

        KeycloakClientNotFoundException ex = await Should.ThrowAsync<KeycloakClientNotFoundException>(
            async () => await provider.GetClientRolesAsync(
                "nonexistent", TestContext.Current.CancellationToken));

        ex.ClientId.ShouldBe("nonexistent");
    }

    [Fact]
    public async Task GetUserClientRolesAsync_ResolvesUuid_ThenListsMappings()
    {
        const string resolveResponse = """[{"id":"client-uuid","clientId":"app-a"}]""";
        const string rolesResponse = """[{"id":"r1","name":"admin","description":null}]""";
        KeycloakIdentityProvider provider = BuildProvider(resolveResponse, rolesResponse);

        IReadOnlyList<IdentityRole> roles = await provider.GetUserClientRolesAsync(
            "user-42", "app-a", TestContext.Current.CancellationToken);

        roles.Count.ShouldBe(1);
        roles[0].Name.ShouldBe("admin");
        roles[0].ClientId.ShouldBe("app-a");
    }

    // ──── ADR-031 — client-role writes ────────────────────────────────────

    [Fact]
    public async Task CreateClientRoleAsync_Resolves_PostsRole_Refetches()
    {
        const string resolveResponse = """[{"id":"client-uuid","clientId":"app-a"}]""";
        // 2nd response = POST /roles (empty 201), 3rd = re-fetch returning the new role.
        const string postResponse = "{}";
        const string refetchResponse = """{"id":"role-new","name":"editor","description":"Edit"}""";
        KeycloakIdentityProvider provider = BuildProvider(resolveResponse, postResponse, refetchResponse);

        IdentityRole created = await provider.CreateClientRoleAsync(
            "app-a", "editor", "Edit", TestContext.Current.CancellationToken);

        created.Id.ShouldBe("role-new");
        created.Name.ShouldBe("editor");
        created.ClientId.ShouldBe("app-a");
        created.Description.ShouldBe("Edit");
    }

    [Fact]
    public async Task AssignClientRoleAsync_LooksUpRole_PostsMapping()
    {
        const string resolveResponse = """[{"id":"client-uuid","clientId":"app-a"}]""";
        const string roleResponse = """{"id":"r1","name":"editor","description":null}""";
        // 3rd response = POST /role-mappings, body ignored by mock.
        const string postResponse = "{}";
        KeycloakIdentityProvider provider = BuildProvider(resolveResponse, roleResponse, postResponse);

        // Should not throw.
        await provider.AssignClientRoleAsync(
            "user-42", "app-a", "editor", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RemoveClientRoleAsync_LooksUpRole_DeletesMapping()
    {
        const string resolveResponse = """[{"id":"client-uuid","clientId":"app-a"}]""";
        const string roleResponse = """{"id":"r1","name":"editor","description":null}""";
        const string deleteResponse = "{}";
        KeycloakIdentityProvider provider = BuildProvider(resolveResponse, roleResponse, deleteResponse);

        await provider.RemoveClientRoleAsync(
            "user-42", "app-a", "editor", TestContext.Current.CancellationToken);
    }
}
