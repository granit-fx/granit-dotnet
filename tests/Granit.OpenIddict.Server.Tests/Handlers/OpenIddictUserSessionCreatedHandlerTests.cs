using System.Security.Claims;
using Granit.Events;
using Granit.OpenIddict.Server.Handlers;
using Granit.Timing;
using Granit.UserSessions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Shouldly;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Granit.OpenIddict.Server.Tests.Handlers;

/// <summary>
/// Unit tests for <see cref="OpenIddictUserSessionCreatedHandler"/> — the OIDC server handler that publishes
/// <see cref="UserSessionCreatedEto"/> when the token endpoint issues a refresh token. Verifies the emit
/// conditions (token endpoint, refresh token issued, not a rotation) and the carried payload.
/// </summary>
public sealed class OpenIddictUserSessionCreatedHandlerTests
{
    private const string UserId = "user-1";
    private const string SessionId = "refresh-token-id";
    private const string TenantId = "11111111-2222-3333-4444-555555555555";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly IDistributedEventBus _bus = Substitute.For<IDistributedEventBus>();

    [Fact]
    public async Task TokenEndpoint_RefreshTokenIssued_PublishesEto()
    {
        ProcessSignInContext context = BuildContext(
            grantType: OpenIddictConstants.GrantTypes.AuthorizationCode,
            refreshTokenId: SessionId,
            tenantId: TenantId);

        await HandlerWithBus().HandleAsync(context);

        await _bus.Received(1).PublishAsync(
            Arg.Is<UserSessionCreatedEto>(e =>
                e.UserId == UserId
                && e.SessionId == SessionId
                && e.Source == UserSessionSource.OpenIddict
                && e.TenantId == Guid.Parse(TenantId)
                && e.CreatedAt == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshTokenGrant_IsRotation_DoesNotPublish()
    {
        ProcessSignInContext context = BuildContext(
            grantType: OpenIddictConstants.GrantTypes.RefreshToken,
            refreshTokenId: SessionId);

        await HandlerWithBus().HandleAsync(context);

        await _bus.DidNotReceiveWithAnyArgs().PublishAsync<UserSessionCreatedEto>(default!, Ct);
    }

    [Fact]
    public async Task NoRefreshTokenIssued_DoesNotPublish()
    {
        // e.g. client_credentials, or a request without offline_access — no session exists.
        ProcessSignInContext context = BuildContext(
            grantType: OpenIddictConstants.GrantTypes.ClientCredentials,
            refreshTokenId: null);

        await HandlerWithBus().HandleAsync(context);

        await _bus.DidNotReceiveWithAnyArgs().PublishAsync<UserSessionCreatedEto>(default!, Ct);
    }

    [Fact]
    public async Task NonTokenEndpoint_DoesNotPublish()
    {
        // The authorization endpoint's sign-in issues a code, not a session.
        ProcessSignInContext context = BuildContext(
            grantType: OpenIddictConstants.GrantTypes.AuthorizationCode,
            refreshTokenId: SessionId,
            endpointType: OpenIddictServerEndpointType.Authorization);

        await HandlerWithBus().HandleAsync(context);

        await _bus.DidNotReceiveWithAnyArgs().PublishAsync<UserSessionCreatedEto>(default!, Ct);
    }

    [Fact]
    public async Task NoDistributedBus_DoesNotThrow()
    {
        ProcessSignInContext context = BuildContext(
            grantType: OpenIddictConstants.GrantTypes.AuthorizationCode,
            refreshTokenId: SessionId);

        OpenIddictUserSessionCreatedHandler handler = new(
            Substitute.For<IServiceProvider>(), Clock(), NullLogger<OpenIddictUserSessionCreatedHandler>.Instance);

        await Should.NotThrowAsync(async () => await handler.HandleAsync(context));
    }

    private OpenIddictUserSessionCreatedHandler HandlerWithBus()
    {
        IServiceProvider services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IDistributedEventBus)).Returns(_bus);
        return new(services, Clock(), NullLogger<OpenIddictUserSessionCreatedHandler>.Instance);
    }

    private static IClock Clock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        return clock;
    }

    private static ProcessSignInContext BuildContext(
        string? grantType,
        string? refreshTokenId,
        string? tenantId = null,
        OpenIddictServerEndpointType endpointType = OpenIddictServerEndpointType.Token)
    {
        OpenIddictServerTransaction transaction = new()
        {
            Request = new OpenIddictRequest { GrantType = grantType },
            Response = new OpenIddictResponse(),
            EndpointType = endpointType,
        };

        List<Claim> claims = [new Claim(OpenIddictConstants.Claims.Subject, UserId)];
        if (tenantId is not null)
        {
            claims.Add(new Claim("tenant_id", tenantId));
        }

        ClaimsPrincipal? refreshTokenPrincipal = refreshTokenId is null
            ? null
            : new ClaimsPrincipal(new ClaimsIdentity("Test")).SetTokenId(refreshTokenId);

        return new ProcessSignInContext(transaction)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            RefreshTokenPrincipal = refreshTokenPrincipal,
        };
    }
}
