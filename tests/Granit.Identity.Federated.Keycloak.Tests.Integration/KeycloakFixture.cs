using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Granit.Testing.Containers;
using Testcontainers.Keycloak;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests.Integration;

/// <summary>
/// Shared fixture — starts a single <c>quay.io/keycloak/keycloak:26.0</c> container
/// for the whole test collection and sets up a deterministic realm:
/// <list type="bullet">
/// <item><description>Realm <c>granit-test</c> imported from
/// <c>Realms/test-realm.json</c> with admin client <c>granit-admin</c> (service-account
/// enabled) and app clients <c>showcase-admin</c> (3 client roles) and
/// <c>showcase-patient</c> (untracked).</description></item>
/// <item><description>Post-import: <c>service-account-granit-admin</c> is granted
/// the five <c>realm-management</c> roles the client-role sync needs
/// (<c>view-clients</c>, <c>query-clients</c>, <c>view-realm</c>, <c>view-users</c>,
/// <c>query-users</c>) via the master admin REST API.</description></item>
/// <item><description>Post-import: user <c>alice</c> is created and assigned two
/// <c>showcase-admin</c> client roles (<c>admin</c>, <c>editor</c>) so
/// <c>GetUserClientRolesAsync</c> has something to return.</description></item>
/// </list>
/// The two "post-import" steps are done programmatically (not in the realm JSON)
/// because Keycloak 26.0's <c>--import-realm</c> path refuses some user / client-role
/// combinations at boot with an opaque "Session not bound to a realm" error — the
/// admin REST API is strictly more reliable.
/// </summary>
public sealed class KeycloakFixture : IAsyncLifetime
{
    public const string Realm = "granit-test";
    public const string AdminClientId = "granit-admin";
    public const string AdminClientSecret = "granit-admin-secret";

    public const string TrackedClientId = "showcase-admin";
    public const string UntrackedClientId = "showcase-patient";
    public const string MissingClientId = "does-not-exist";

    public const string TestUsername = "alice";

    private const string MasterAdmin = "admin";
    private const string MasterAdminPassword = "admin";

    private KeycloakContainer _container = null!;

    public string BaseUrl => _container.GetBaseAddress();

    /// <summary>
    /// Builds a fresh <see cref="HttpClient"/> authenticated as the master admin, suitable
    /// for tests that need to mutate Keycloak state (update role description, delete a
    /// client role, etc.). Caller is responsible for disposal.
    /// </summary>
    public async Task<HttpClient> CreateMasterAdminHttpClientAsync()
    {
        HttpClient http = new() { BaseAddress = new Uri(BaseUrl) };
        string token = await GetMasterAdminTokenAsync(http);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    public async ValueTask InitializeAsync()
    {
        string realmPath = Path.Combine(AppContext.BaseDirectory, "Realms", "test-realm.json");

        _container = new KeycloakBuilder("quay.io/keycloak/keycloak:25.0")
            .WithUsername(MasterAdmin)
            .WithPassword(MasterAdminPassword)
            .WithRealm(realmPath)
            .Build();

        await ContainerStartRetry.RunWithRetryAsync(
            ct => _container.StartAsync(ct),
            label: "keycloak-integration-fixture");

        await ConfigurePostImportAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private async Task ConfigurePostImportAsync()
    {
        using HttpClient http = new() { BaseAddress = new Uri(BaseUrl) };
        string token = await GetMasterAdminTokenAsync(http);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Grant realm-management client roles to the service-account user of granit-admin.
        string realmManagementUuid = await ResolveClientUuidAsync(http, "realm-management");
        string granitAdminClientUuid = await ResolveClientUuidAsync(http, AdminClientId);
        string saUserId = await GetServiceAccountUserIdAsync(http, granitAdminClientUuid);

        string[] managementRoles =
        [
            "view-clients", "query-clients", "view-realm",
            "view-users", "query-users",
        ];
        List<RoleRepresentation> rolesToAssign = [];
        foreach (string roleName in managementRoles)
        {
            RoleRepresentation? role = await http.GetFromJsonAsync<RoleRepresentation>(
                $"admin/realms/{Realm}/clients/{realmManagementUuid}/roles/{Uri.EscapeDataString(roleName)}");
            rolesToAssign.Add(role!);
        }

        using HttpResponseMessage grant = await http.PostAsJsonAsync(
            $"admin/realms/{Realm}/users/{saUserId}/role-mappings/clients/{realmManagementUuid}",
            rolesToAssign);
        grant.EnsureSuccessStatusCode();

        // 2. Create alice and assign her 2 showcase-admin client roles.
        UserRepresentation alice = new(TestUsername, Enabled: true);
        using HttpResponseMessage createAlice = await http.PostAsJsonAsync(
            $"admin/realms/{Realm}/users", alice);
        createAlice.EnsureSuccessStatusCode();

        string aliceId = await ResolveUserIdAsync(http, TestUsername);

        string showcaseAdminUuid = await ResolveClientUuidAsync(http, TrackedClientId);
        List<RoleRepresentation> aliceRoles = [];
        foreach (string roleName in new[] { "admin", "editor" })
        {
            RoleRepresentation? role = await http.GetFromJsonAsync<RoleRepresentation>(
                $"admin/realms/{Realm}/clients/{showcaseAdminUuid}/roles/{Uri.EscapeDataString(roleName)}");
            aliceRoles.Add(role!);
        }

        using HttpResponseMessage assign = await http.PostAsJsonAsync(
            $"admin/realms/{Realm}/users/{aliceId}/role-mappings/clients/{showcaseAdminUuid}",
            aliceRoles);
        assign.EnsureSuccessStatusCode();
    }

    private static async Task<string> GetMasterAdminTokenAsync(HttpClient http)
    {
        Dictionary<string, string> body = new()
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = MasterAdmin,
            ["password"] = MasterAdminPassword,
        };
        using HttpResponseMessage response = await http.PostAsync(
            "realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(body));
        response.EnsureSuccessStatusCode();

        TokenResponse? token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return token!.AccessToken;
    }

    private static async Task<string> ResolveClientUuidAsync(HttpClient http, string clientId)
    {
        List<ClientRepresentation>? clients = await http.GetFromJsonAsync<List<ClientRepresentation>>(
            $"admin/realms/{Realm}/clients?clientId={Uri.EscapeDataString(clientId)}");
        return clients!.Single().Id;
    }

    private static async Task<string> ResolveUserIdAsync(HttpClient http, string username)
    {
        List<UserRepresentation>? users = await http.GetFromJsonAsync<List<UserRepresentation>>(
            $"admin/realms/{Realm}/users?username={Uri.EscapeDataString(username)}&exact=true");
        return users!.Single().Id!;
    }

    private static async Task<string> GetServiceAccountUserIdAsync(HttpClient http, string clientUuid)
    {
        UserRepresentation? sa = await http.GetFromJsonAsync<UserRepresentation>(
            $"admin/realms/{Realm}/clients/{clientUuid}/service-account-user");
        return sa!.Id!;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record ClientRepresentation(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("clientId")] string ClientId);

    private sealed record RoleRepresentation(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record UserRepresentation(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("enabled")] bool Enabled)
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }
}

/// <summary>
/// Marker collection so every test class in this project shares a single
/// <see cref="KeycloakFixture"/> instance — one Keycloak container is enough for all
/// 7 scenarios.
/// </summary>
[CollectionDefinition(Name)]
public sealed class KeycloakTests : ICollectionFixture<KeycloakFixture>
{
    public const string Name = "Keycloak";
}
