using System.Net.Http.Headers;
using System.Net.Http.Json;
using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Events;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Extensions;
using Granit.Identity.Federated.Keycloak.Extensions;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Identity.Federated.Keycloak.Sync;
using Granit.Identity.Models;
using Granit.Timing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests.Integration;

/// <summary>
/// Seven end-to-end scenarios exercising <see cref="KeycloakClientRoleSyncService"/>
/// and <see cref="IIdentityClientRoleManager"/> against the real Keycloak 26.0 admin REST
/// API. The fixture (<see cref="KeycloakFixture"/>) seeds a deterministic realm with three
/// client roles on <c>showcase-admin</c> and a user (<c>alice</c>) holding two of them.
/// </summary>
[Collection(KeycloakTests.Name)]
public sealed class KeycloakClientRoleSyncTests(KeycloakFixture keycloak)
{
    private readonly KeycloakFixture _keycloak = keycloak;

    private ServiceProvider BuildServices(
        InMemoryRoleMetadataStore? sharedStore = null,
        params string[] trackedClientIds)
    {
        ServiceCollection services = [];
        Dictionary<string, string?> config = new()
        {
            ["KeycloakAdmin:BaseUrl"] = _keycloak.BaseUrl,
            ["KeycloakAdmin:Realm"] = KeycloakFixture.Realm,
            ["KeycloakAdmin:ClientId"] = KeycloakFixture.AdminClientId,
            ["KeycloakAdmin:ClientSecret"] = KeycloakFixture.AdminClientSecret,
        };
        for (int i = 0; i < trackedClientIds.Length; i++)
        {
            config[$"KeycloakAdmin:ClientRoleSync:TrackedClientIds:{i}"] = trackedClientIds[i];
        }

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(config).Build());
        services.AddLogging();
        services.AddSingleton(Substitute.For<IClock>());
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        services.AddSingleton<IGuidGenerator, SimpleGuidGenerator>();

        services.AddGranitIdentity();
        services.AddGranitIdentityKeycloak();

        // Replace the EF-backed RoleMetadata store with an in-memory one — the Keycloak
        // HTTP path is what we want to validate, not EF mapping (already covered in
        // Granit.Authorization.EntityFrameworkCore.Tests.Integration).
        services.RemoveAll<IRoleMetadataStore>();
        services.AddSingleton<IRoleMetadataStore>(sharedStore ?? new InMemoryRoleMetadataStore());

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SyncAsync_HappyPath_PersistsRoleMetadataWithClientId()
    {
        InMemoryRoleMetadataStore store = new();
        await using ServiceProvider sp = BuildServices(store, KeycloakFixture.TrackedClientId);
        KeycloakClientRoleSyncService sut = sp.GetRequiredService<KeycloakClientRoleSyncService>();

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        store.All.Count.ShouldBe(3);
        store.All.ShouldAllBe(r => r.ClientId == KeycloakFixture.TrackedClientId);
        store.All.Select(r => r.Name).OrderBy(x => x).ShouldBe(["admin", "editor", "viewer"]);
        store.All.First(r => r.Name == "editor").Description.ShouldBe("Edit showcase documents");
    }

    [Fact]
    public async Task SyncAsync_RerunAfterNoChanges_IsIdempotent()
    {
        InMemoryRoleMetadataStore store = new();
        await using ServiceProvider sp = BuildServices(store, KeycloakFixture.TrackedClientId);
        KeycloakClientRoleSyncService sut = sp.GetRequiredService<KeycloakClientRoleSyncService>();

        await sut.SyncAsync(TestContext.Current.CancellationToken);
        int firstRunCount = store.All.Count;
        IReadOnlyList<Guid> firstRunIds = store.All.Select(r => r.Id).OrderBy(x => x).ToList();

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        store.All.Count.ShouldBe(firstRunCount);
        store.All.Select(r => r.Id).OrderBy(x => x).ShouldBe(firstRunIds);
    }

    [Fact]
    public async Task SyncAsync_DescriptionChanged_UpdatesExistingRow()
    {
        InMemoryRoleMetadataStore store = new();
        await using ServiceProvider sp = BuildServices(store, KeycloakFixture.TrackedClientId);
        KeycloakClientRoleSyncService sut = sp.GetRequiredService<KeycloakClientRoleSyncService>();

        // First sync — capture the current state.
        await sut.SyncAsync(TestContext.Current.CancellationToken);
        RoleMetadata editor = store.All.Single(r => r.Name == "editor");
        Guid originalId = editor.Id;

        // Mutate the description in Keycloak.
        await UpdateClientRoleDescriptionAsync(
            clientId: KeycloakFixture.TrackedClientId,
            roleName: "editor",
            newDescription: "Edit showcase documents — updated");
        try
        {
            // Second sync — must update the existing row in-place, not insert a new one.
            await sut.SyncAsync(TestContext.Current.CancellationToken);

            RoleMetadata reloaded = store.All.Single(r => r.Name == "editor");
            reloaded.Id.ShouldBe(originalId);
            reloaded.Description.ShouldBe("Edit showcase documents — updated");
        }
        finally
        {
            // Restore the original description so other tests see the seeded state.
            await UpdateClientRoleDescriptionAsync(
                KeycloakFixture.TrackedClientId,
                "editor",
                "Edit showcase documents");
        }
    }

    [Fact]
    public async Task SyncAsync_UntrackedClient_NoRowsForIt()
    {
        InMemoryRoleMetadataStore store = new();
        await using ServiceProvider sp = BuildServices(store, KeycloakFixture.TrackedClientId);
        KeycloakClientRoleSyncService sut = sp.GetRequiredService<KeycloakClientRoleSyncService>();

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        store.All.ShouldNotContain(r => r.ClientId == KeycloakFixture.UntrackedClientId);
    }

    [Fact]
    public async Task SyncAsync_MissingTrackedClient_LogsWarningAndContinues()
    {
        InMemoryRoleMetadataStore store = new();
        await using ServiceProvider sp = BuildServices(
            store,
            KeycloakFixture.MissingClientId,
            KeycloakFixture.TrackedClientId);
        KeycloakClientRoleSyncService sut = sp.GetRequiredService<KeycloakClientRoleSyncService>();

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        // Missing client is skipped; the next tracked client still sync'd successfully.
        store.All.Count(r => r.ClientId == KeycloakFixture.TrackedClientId).ShouldBe(3);
    }

    [Fact]
    public async Task GetUserClientRolesAsync_UserWithAssignments_Returns_IdentityRoles()
    {
        await using ServiceProvider sp = BuildServices();
        IIdentityClientRoleManager manager = sp.GetRequiredService<IIdentityClientRoleManager>();
        IIdentityProvider provider = sp.GetRequiredService<IIdentityProvider>();

        IReadOnlyList<IIdentityUser> users = await provider.GetUsersAsync(
            search: KeycloakFixture.TestUsername,
            cancellationToken: TestContext.Current.CancellationToken);
        users.Count.ShouldBeGreaterThan(0, "fixture must expose user 'alice' via GetUsersAsync");
        IIdentityUser alice = users[0];

        IReadOnlyList<IdentityRole> roles = await manager.GetUserClientRolesAsync(
            alice.UserId,
            KeycloakFixture.TrackedClientId,
            TestContext.Current.CancellationToken);

        roles.Count.ShouldBe(2);
        roles.ShouldAllBe(r => r.ClientId == KeycloakFixture.TrackedClientId);
        roles.Select(r => r.Name).OrderBy(x => x).ShouldBe(["admin", "editor"]);
    }

    [Fact]
    public async Task SyncAsync_ClientRoleDeletedInKeycloak_RoleMetadataLeftAlone()
    {
        InMemoryRoleMetadataStore store = new();
        await using ServiceProvider sp = BuildServices(store, KeycloakFixture.TrackedClientId);
        KeycloakClientRoleSyncService sut = sp.GetRequiredService<KeycloakClientRoleSyncService>();

        await sut.SyncAsync(TestContext.Current.CancellationToken);
        store.All.Count.ShouldBe(3);

        // Delete a role directly in Keycloak, then restore it so the shared fixture stays
        // in a known state for other tests (tests in the collection run serially against
        // the same container — no test may leave the realm permanently mutated).
        await DeleteClientRoleAsync(KeycloakFixture.TrackedClientId, roleName: "viewer");
        try
        {
            await sut.SyncAsync(TestContext.Current.CancellationToken);

            // No-delete policy: the orphaned row survives. Only 'admin' + 'editor' come back
            // from Keycloak, but 'viewer' stays in the store until an explicit cleanup job
            // (tracked separately in Phase 3, #1118).
            store.All.Count.ShouldBe(3);
            store.All.Select(r => r.Name).OrderBy(x => x).ShouldBe(["admin", "editor", "viewer"]);
        }
        finally
        {
            await RestoreClientRoleAsync(
                KeycloakFixture.TrackedClientId,
                name: "viewer",
                description: "Read showcase documents");
        }
    }

    // ──────── Keycloak admin helpers used by the drift / delete scenarios ────────

    private static async Task<string> ResolveClientUuidAsync(HttpClient client, string clientId)
    {
        List<ClientDto>? clients = await client.GetFromJsonAsync<List<ClientDto>>(
            $"admin/realms/{KeycloakFixture.Realm}/clients?clientId={Uri.EscapeDataString(clientId)}");
        return clients!.Single().Id;
    }

    private async Task UpdateClientRoleDescriptionAsync(
        string clientId, string roleName, string newDescription)
    {
        using HttpClient client = await _keycloak.CreateMasterAdminHttpClientAsync();
        string uuid = await ResolveClientUuidAsync(client, clientId);

        RoleDto? existing = await client.GetFromJsonAsync<RoleDto>(
            $"admin/realms/{KeycloakFixture.Realm}/clients/{uuid}/roles/{Uri.EscapeDataString(roleName)}");
        existing = existing! with { Description = newDescription };

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            $"admin/realms/{KeycloakFixture.Realm}/clients/{uuid}/roles/{Uri.EscapeDataString(roleName)}",
            existing);
        response.EnsureSuccessStatusCode();
    }

    private async Task DeleteClientRoleAsync(string clientId, string roleName)
    {
        using HttpClient client = await _keycloak.CreateMasterAdminHttpClientAsync();
        string uuid = await ResolveClientUuidAsync(client, clientId);
        using HttpResponseMessage response = await client.DeleteAsync(
            $"admin/realms/{KeycloakFixture.Realm}/clients/{uuid}/roles/{Uri.EscapeDataString(roleName)}");
        response.EnsureSuccessStatusCode();
    }

    private async Task RestoreClientRoleAsync(string clientId, string name, string description)
    {
        using HttpClient client = await _keycloak.CreateMasterAdminHttpClientAsync();
        string uuid = await ResolveClientUuidAsync(client, clientId);
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"admin/realms/{KeycloakFixture.Realm}/clients/{uuid}/roles",
            new { name, description, clientRole = true });
        response.EnsureSuccessStatusCode();
    }

    private sealed record ClientDto(
        [property: System.Text.Json.Serialization.JsonPropertyName("id")] string Id,
        [property: System.Text.Json.Serialization.JsonPropertyName("clientId")] string ClientId);

    private sealed record RoleDto(
        [property: System.Text.Json.Serialization.JsonPropertyName("id")] string Id,
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("description")] string? Description)
    {
        [System.Text.Json.Serialization.JsonPropertyName("clientRole")]
        public bool ClientRole { get; init; } = true;
    }
}
