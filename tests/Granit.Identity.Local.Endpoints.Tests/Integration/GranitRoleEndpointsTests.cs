using System.Net;
using System.Net.Http.Json;
using FluentValidation;
using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Extensions;
using Granit.Identity.Local.Endpoints.Options;
using Granit.Identity.Local.Endpoints.Permissions;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests.Integration;

/// <summary>
/// HTTP-level tests for the <c>/admin/roles</c> CRUD endpoints (list, get, create, rename,
/// delete) driving mocked <see cref="IRoleMetadataStore"/> / <see cref="IGranitRoleOrchestrator"/>.
/// The caller is a host admin (no current tenant), so the full visibility matrix is exercised.
/// </summary>
public sealed class GranitRoleEndpointsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Rig : IAsyncDisposable
    {
        public required WebApplication App { get; init; }
        public required HttpClient Client { get; init; }
        public required IRoleMetadataStore Store { get; init; }
        public required IGranitRoleOrchestrator Orchestrator { get; init; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static RoleMetadata Role(string name = "editor", bool isSystem = false) =>
        RoleMetadata.Create(Guid.NewGuid(), name, MultiTenancySides.Both, tenantId: null, isSystem: isSystem);

    private static async Task<Rig> CreateAsync()
    {
        IRoleMetadataStore store = Substitute.For<IRoleMetadataStore>();
        IGranitRoleOrchestrator orchestrator = Substitute.For<IGranitRoleOrchestrator>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>(); // IsAvailable=false => host admin

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(IdentityLocalPermissions.Roles.Read,
                p => p.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityLocalPermissions.Roles.Read))
            .AddPolicy(IdentityLocalPermissions.Roles.Manage,
                p => p.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityLocalPermissions.Roles.Manage))
            .AddPolicy(IdentityLocalPermissions.Roles.Delete,
                p => p.RequireClaim(TestAuthHandler.PermissionClaimType, IdentityLocalPermissions.Roles.Delete));

        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(orchestrator);
        builder.Services.AddSingleton(currentTenant);
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new RoleEndpointsOptions()));
        builder.Services.AddValidatorsFromAssemblyContaining<AccountEndpointsOptions>(
            ServiceLifetime.Singleton, includeInternalTypes: true);

        WebApplication app = builder.Build();
        app.MapGranitRoles();
        await app.StartAsync().ConfigureAwait(false);

        HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.PermissionsHeader,
            $"{IdentityLocalPermissions.Roles.Read},{IdentityLocalPermissions.Roles.Manage},{IdentityLocalPermissions.Roles.Delete}");

        return new Rig { App = app, Client = client, Store = store, Orchestrator = orchestrator };
    }

    [Fact]
    public async Task List_HostAdmin_Returns200WithAllRoles()
    {
        await using Rig rig = await CreateAsync();
        rig.Store.ListAllAsync(Arg.Any<CancellationToken>()).Returns([Role("a"), Role("b")]);

        HttpResponseMessage response = await rig.Client.GetAsync("/admin/roles", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<RoleResponse>? body = await response.Content.ReadFromJsonAsync<IReadOnlyList<RoleResponse>>(Ct);
        body!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetById_Existing_Returns200()
    {
        await using Rig rig = await CreateAsync();
        RoleMetadata role = Role("editor");
        rig.Store.FindByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        HttpResponseMessage response = await rig.Client.GetAsync($"/admin/roles/{role.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        RoleResponse? body = await response.Content.ReadFromJsonAsync<RoleResponse>(Ct);
        body!.Name.ShouldBe("editor");
    }

    [Fact]
    public async Task GetById_Missing_Returns404()
    {
        await using Rig rig = await CreateAsync();
        rig.Store.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((RoleMetadata?)null);

        HttpResponseMessage response = await rig.Client.GetAsync($"/admin/roles/{Guid.NewGuid()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201()
    {
        await using Rig rig = await CreateAsync();
        rig.Orchestrator.CreateAsync(Arg.Any<CreateRoleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Role("auditor"));

        HttpResponseMessage response = await rig.Client.PostAsJsonAsync(
            "/admin/roles", new RoleCreateRequest("auditor", MultiTenancySides.Both, TenantId: null), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        await using Rig rig = await CreateAsync();
        rig.Orchestrator.CreateAsync(Arg.Any<CreateRoleCommand>(), Arg.Any<CancellationToken>())
            .Returns<RoleMetadata>(_ => throw new InvalidOperationException("Role already exists."));

        HttpResponseMessage response = await rig.Client.PostAsJsonAsync(
            "/admin/roles", new RoleCreateRequest("dupe", MultiTenancySides.Both, TenantId: null), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rename_Existing_Returns200()
    {
        await using Rig rig = await CreateAsync();
        RoleMetadata role = Role("old");
        rig.Store.FindByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);
        rig.Orchestrator.RenameAsync(role.Id, "new", Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Role("new"));

        HttpResponseMessage response = await rig.Client.PutAsJsonAsync(
            $"/admin/roles/{role.Id}", new RoleUpdateRequest("new", "v1"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rename_Missing_Returns404()
    {
        await using Rig rig = await CreateAsync();
        rig.Store.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((RoleMetadata?)null);

        HttpResponseMessage response = await rig.Client.PutAsJsonAsync(
            $"/admin/roles/{Guid.NewGuid()}", new RoleUpdateRequest("x", "stamp"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Existing_Returns204()
    {
        await using Rig rig = await CreateAsync();
        RoleMetadata role = Role("obsolete");
        rig.Store.FindByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        HttpResponseMessage response = await rig.Client.DeleteAsync($"/admin/roles/{role.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await rig.Orchestrator.Received(1).DeleteAsync(role.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_SystemRole_Returns403()
    {
        await using Rig rig = await CreateAsync();
        RoleMetadata system = Role("admin", isSystem: true);
        rig.Store.FindByIdAsync(system.Id, Arg.Any<CancellationToken>()).Returns(system);

        HttpResponseMessage response = await rig.Client.DeleteAsync($"/admin/roles/{system.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await rig.Orchestrator.DidNotReceiveWithAnyArgs().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Missing_Returns404()
    {
        await using Rig rig = await CreateAsync();
        rig.Store.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((RoleMetadata?)null);

        HttpResponseMessage response = await rig.Client.DeleteAsync($"/admin/roles/{Guid.NewGuid()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
