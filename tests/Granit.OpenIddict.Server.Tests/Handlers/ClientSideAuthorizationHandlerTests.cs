using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Json;
using Granit.MultiTenancy;
using Granit.OpenIddict.Extensions;
using Granit.OpenIddict.Server.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Shouldly;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Granit.OpenIddict.Server.Tests.Handlers;

/// <summary>
/// Unit tests for <see cref="ClientSideAuthorizationHandler"/> — the OIDC server
/// handler that enforces the <see cref="MultiTenancySides"/> policy declared on
/// each application. Verifies the full policy matrix (Host/Tenant/Both/null ×
/// host-user/tenant-user) and the carve-outs for service-to-service flows.
/// </summary>
public sealed class ClientSideAuthorizationHandlerTests
{
    private const string ClientId = "test-client";
    private const string TenantId = "11111111-2222-3333-4444-555555555555";

    [Fact]
    public async Task HostOnly_HostUser_Allowed()
    {
        ProcessSignInContext context = await RunAsync(
            clientSide: MultiTenancySides.Host,
            userTenantId: null);

        context.IsRejected.ShouldBeFalse();
    }

    [Fact]
    public async Task HostOnly_TenantUser_Rejected()
    {
        ProcessSignInContext context = await RunAsync(
            clientSide: MultiTenancySides.Host,
            userTenantId: TenantId);

        context.IsRejected.ShouldBeTrue();
        context.Error.ShouldBe(OpenIddictConstants.Errors.AccessDenied);
    }

    [Fact]
    public async Task TenantOnly_HostUser_Rejected()
    {
        ProcessSignInContext context = await RunAsync(
            clientSide: MultiTenancySides.Tenant,
            userTenantId: null);

        context.IsRejected.ShouldBeTrue();
        context.Error.ShouldBe(OpenIddictConstants.Errors.AccessDenied);
    }

    [Fact]
    public async Task TenantOnly_TenantUser_Allowed()
    {
        ProcessSignInContext context = await RunAsync(
            clientSide: MultiTenancySides.Tenant,
            userTenantId: TenantId);

        context.IsRejected.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TenantId)]
    public async Task Both_AnyUser_Allowed(string? userTenantId)
    {
        ProcessSignInContext context = await RunAsync(
            clientSide: MultiTenancySides.Both,
            userTenantId: userTenantId);

        context.IsRejected.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TenantId)]
    public async Task NullPolicy_IsBackwardCompatible_AllowsAnyUser(string? userTenantId)
    {
        ProcessSignInContext context = await RunAsync(
            clientSide: null,
            userTenantId: userTenantId);

        context.IsRejected.ShouldBeFalse();
    }

    [Fact]
    public async Task ClientCredentialsGrant_IsNotRestricted_EvenWhenHostOnly()
    {
        // Service-to-service flows carry no user principal. Applying the side
        // policy would lock out any host-only API client that uses client_credentials —
        // the policy is about USERS, not clients.
        ProcessSignInContext context = await RunAsync(
            clientSide: MultiTenancySides.Host,
            userTenantId: TenantId,
            grantType: OpenIddictConstants.GrantTypes.ClientCredentials);

        context.IsRejected.ShouldBeFalse();
    }

    [Fact]
    public async Task NullClientId_IsSkipped()
    {
        // Defensive: the built-in OpenIddict validation handlers reject missing
        // client_id long before we run, but we must never throw if we see a null.
        ProcessSignInContext context = await RunAsync(
            clientSide: MultiTenancySides.Host,
            userTenantId: TenantId,
            clientId: null);

        context.IsRejected.ShouldBeFalse();
    }

    [Fact]
    public async Task UnknownClient_IsSkipped()
    {
        IOpenIddictApplicationManager manager = Substitute.For<IOpenIddictApplicationManager>();
        manager.FindByClientIdAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        ClientSideAuthorizationHandler handler = new(manager, NullLogger<ClientSideAuthorizationHandler>.Instance);
        ProcessSignInContext context = BuildContext(clientId: ClientId, userTenantId: null, grantType: null);

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeFalse();
    }

    private static async Task<ProcessSignInContext> RunAsync(
        MultiTenancySides? clientSide,
        string? userTenantId,
        string? grantType = null,
        string? clientId = ClientId)
    {
        IOpenIddictApplicationManager manager = Substitute.For<IOpenIddictApplicationManager>();
        object app = new();

        manager.FindByClientIdAsync(
                Arg.Is<string>(id => id == clientId),
                Arg.Any<CancellationToken>())
            .Returns(app);

        ImmutableDictionary<string, JsonElement> properties =
            clientSide is null
                ? ImmutableDictionary<string, JsonElement>.Empty
                : ImmutableDictionary<string, JsonElement>.Empty.Add(
                    OpenIddictApplicationClientSideExtensions.ClientSidePropertyKey,
                    JsonSerializer.SerializeToElement(clientSide.Value.ToString()));

        manager.GetPropertiesAsync(app, Arg.Any<CancellationToken>())
            .Returns(properties);

        ClientSideAuthorizationHandler handler = new(manager, NullLogger<ClientSideAuthorizationHandler>.Instance);
        ProcessSignInContext context = BuildContext(clientId, userTenantId, grantType);

        await handler.HandleAsync(context);
        return context;
    }

    private static ProcessSignInContext BuildContext(
        string? clientId, string? userTenantId, string? grantType)
    {
        OpenIddictRequest request = new()
        {
            ClientId = clientId,
            GrantType = grantType,
        };

        OpenIddictServerTransaction transaction = new()
        {
            Request = request,
            Response = new OpenIddictResponse(),
        };

        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, "user-id")];
        if (userTenantId is not null)
        {
            claims.Add(new Claim("tenant_id", userTenantId));
        }

        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "Test"));

        return new ProcessSignInContext(transaction)
        {
            Principal = principal,
        };
    }
}
