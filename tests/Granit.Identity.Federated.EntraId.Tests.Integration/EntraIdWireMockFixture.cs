using System.Net;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests.Integration;

/// <summary>
/// In-process WireMock.Net server that impersonates both the Entra ID token endpoint
/// (<c>https://login.microsoftonline.com/&lt;tenant&gt;/oauth2/v2.0/token</c>) and the
/// Microsoft Graph v1.0 surface (<c>https://graph.microsoft.com/v1.0/*</c>).
/// </summary>
/// <remarks>
/// <para>
/// Graph traffic is redirected to this server by setting <c>GraphBaseUrl</c> on the
/// provider options to the WireMock URL. Token traffic has an absolute URL baked into
/// <see cref="Options.EntraIdAdminOptions.GetTokenEndpoint"/>, so a
/// <see cref="GraphUrlRewritingHandler"/> rewrites <c>login.microsoftonline.com</c> to
/// the same WireMock host before the request leaves the test process.
/// </para>
/// <para>
/// Each test is expected to call <see cref="Reset"/> at the start (or the tests must
/// use disjoint URL mappings). Helper methods
/// <see cref="StubTokenEndpoint"/>, <see cref="StubServicePrincipal"/>, etc. install
/// the canonical stubs used by the 5 scenarios.
/// </para>
/// </remarks>
public sealed class EntraIdWireMockFixture : IAsyncLifetime
{
    public const string TenantId = "11111111-1111-1111-1111-111111111111";
    public const string AdminClientId = "admin-service";
    public const string AdminClientSecret = "secret";
    public const string ServicePrincipalObjectId = "sp-object-id";

    public const string TrackedAppId = "22222222-2222-2222-2222-222222222222";
    public const string TrackedSpObjectId = "sp-tracked-oid";
    public const string UnknownAppId = "99999999-9999-9999-9999-999999999999";
    public const string TestUserId = "user-42";

    private WireMockServer _server = null!;

    public string BaseUrl => _server.Url!;

    public ValueTask InitializeAsync()
    {
        _server = WireMockServer.Start(new WireMockServerSettings
        {
            StartAdminInterface = false,
            UseSSL = false,
        });
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _server.Stop();
        _server.Dispose();
        return ValueTask.CompletedTask;
    }

    public void Reset() => _server.ResetMappings();

    /// <summary>
    /// Stub the OAuth2 token endpoint used by <c>client_credentials</c>. Returns a
    /// fixed access token so every call goes through but only the first triggers a
    /// network hit (the provider caches the token internally).
    /// </summary>
    public void StubTokenEndpoint()
    {
        _server
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath($"/{TenantId}/oauth2/v2.0/token")
                .UsingPost())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"fake-token","expires_in":3600,"token_type":"Bearer"}"""));
    }

    /// <summary>
    /// Stub <c>GET /v1.0/servicePrincipals?$filter=appId eq 'X'</c> — the resolution
    /// call. Pass <paramref name="appRolesJson"/> as a JSON array literal.
    /// </summary>
    public void StubServicePrincipal(string appId, string spObjectId, string appRolesJson)
    {
        string body = $$"""
            {"value":[
              {"id":"{{spObjectId}}","appId":"{{appId}}","appRoles":{{appRolesJson}}}
            ]}
            """;
        _server
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/v1.0/servicePrincipals")
                .WithParam("$filter", $"appId eq '{appId}'")
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }

    /// <summary>
    /// Stub a resolution call that returns an empty collection — the provider treats
    /// this as "appId not found in tenant" and raises <c>EntraIdClientNotFoundException</c>.
    /// </summary>
    public void StubServicePrincipalNotFound(string appId)
    {
        _server
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/v1.0/servicePrincipals")
                .WithParam("$filter", $"appId eq '{appId}'")
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"value":[]}"""));
    }

    /// <summary>
    /// Stub <c>GET /v1.0/users/{id}/appRoleAssignments?$filter=resourceId eq spOid</c>.
    /// Pass <paramref name="assignmentsJson"/> as a JSON array literal.
    /// </summary>
    public void StubUserAppRoleAssignments(string userId, string spObjectId, string assignmentsJson)
    {
        string body = $$"""{"value":{{assignmentsJson}}}""";
        _server
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath($"/v1.0/users/{userId}/appRoleAssignments")
                .WithParam("$filter", $"resourceId eq {spObjectId}")
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }
}
