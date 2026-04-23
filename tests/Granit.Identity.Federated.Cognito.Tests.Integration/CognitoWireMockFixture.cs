using System.Net;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests.Integration;

/// <summary>
/// In-process WireMock.Net server that impersonates the AWS Cognito Identity
/// Provider admin API over its JSON 1.1 wire protocol.
/// </summary>
/// <remarks>
/// <para>
/// The AWS SDK for Cognito posts every operation to the service root URL with
/// the operation name in the <c>X-Amz-Target</c> header
/// (e.g. <c>AWSCognitoIdentityProviderService.ListGroups</c>) and a JSON body
/// shaped like the request DTO. Response codes map to exceptions:
/// 200 → success, 400 + <c>__type</c> = <c>NotAuthorizedException</c>, etc.
/// </para>
/// <para>
/// The SDK still signs requests with SigV4, but WireMock ignores the signature;
/// we only match on <c>X-Amz-Target</c>.
/// </para>
/// </remarks>
public sealed class CognitoWireMockFixture : IAsyncLifetime
{
    public const string Region = "eu-west-1";
    public const string UserPoolId = "eu-west-1_TEST";
    public const string AppClientIdA = "clientA";
    public const string AppClientIdB = "clientB";

    private const string TargetHeader = "X-Amz-Target";
    private const string CognitoService = "AWSCognitoIdentityProviderService";

    private WireMockServer _server = null!;

    public string ServiceUrl => _server.Url!;

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
    /// Stub <c>ListUserPoolClients</c>. Response payload is the AWS SDK shape:
    /// <c>{"UserPoolClients":[{"ClientId":"..","ClientName":"..","UserPoolId":"..",..}]}</c>.
    /// </summary>
    public void StubListUserPoolClients(params string[] clientIds)
    {
        string json = $$"""
            {"UserPoolClients":[
              {{string.Join(',', clientIds.Select(id => $$"""
                {"ClientId":"{{id}}","ClientName":"{{id}}","UserPoolId":"{{UserPoolId}}"}
              """))}}
            ]}
            """;
        StubPostOperation("ListUserPoolClients", HttpStatusCode.OK, json);
    }

    /// <summary>
    /// Stub <c>ListGroups</c>. Each tuple is <c>(groupName, description)</c>.
    /// </summary>
    public void StubListGroups(params (string Name, string? Description)[] groups)
    {
        string body = BuildGroupsPayload(groups);
        StubPostOperation("ListGroups", HttpStatusCode.OK, body);
    }

    /// <summary>
    /// Stub <c>AdminListGroupsForUser</c>. Each tuple is <c>(groupName, description)</c>.
    /// </summary>
    public void StubAdminListGroupsForUser(params (string Name, string? Description)[] groups)
    {
        string body = BuildGroupsPayload(groups);
        StubPostOperation("AdminListGroupsForUser", HttpStatusCode.OK, body);
    }

    /// <summary>
    /// Stub <c>ListGroups</c> so the SDK surfaces a <c>NotAuthorizedException</c>.
    /// </summary>
    public void StubListGroupsNotAuthorized()
    {
        const string errorBody = """
            {"__type":"NotAuthorizedException","message":"Not authorized to perform this operation."}
            """;
        StubPostOperation("ListGroups", HttpStatusCode.BadRequest, errorBody);
    }

    private static string BuildGroupsPayload((string Name, string? Description)[] groups)
    {
        static string Json(string s) => System.Text.Json.JsonSerializer.Serialize(s);

        string[] entries = groups
            .Select(g => g.Description is null
                ? $$"""{"GroupName":{{Json(g.Name)}},"UserPoolId":"{{UserPoolId}}"}"""
                : $$"""{"GroupName":{{Json(g.Name)}},"Description":{{Json(g.Description)}},"UserPoolId":"{{UserPoolId}}"}""")
            .ToArray();

        return $$"""{"Groups":[{{string.Join(',', entries)}}]}""";
    }

    private void StubPostOperation(string operation, HttpStatusCode status, string body)
    {
        _server
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/")
                .UsingPost()
                .WithHeader(TargetHeader, $"{CognitoService}.{operation}"))
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(status)
                .WithHeader("Content-Type", "application/x-amz-json-1.1")
                .WithBody(body));
    }
}
