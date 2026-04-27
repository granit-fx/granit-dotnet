using Granit.Invoicing.Odoo.Internal;
using Granit.Invoicing.Odoo.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Invoicing.Odoo.Tests.Internal;

public sealed class OdooJsonRpcClientTests : IDisposable
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _factory = Substitute.For<IHttpClientFactory>();

    private static readonly OdooOptions DefaultOpts = new()
    {
        BaseUrl = "https://test.odoo.com",
        Database = "testdb",
        ApiKey = "test-key",
        Login = "user@test.com",
        DefaultJournalId = 1,
    };

    public OdooJsonRpcClientTests()
    {
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://test.odoo.com/") };
        _factory.CreateClient("Odoo").Returns(_httpClient);
    }

    public void Dispose() => _httpClient.Dispose();

    private OdooJsonRpcClient Build(OdooOptions? opts = null) =>
        new(_factory, MsOptions.Create(opts ?? DefaultOpts), NullLogger<OdooJsonRpcClient>.Instance);

    private void EnqueueAuthSuccess(int uid = 42) =>
        _handler.ResponseQueue.Enqueue($$"""{"result":{{uid}}}""");

    [Fact]
    public async Task AuthenticateAsync_FirstCall_PostsCredentialsAndReturnsUid()
    {
        EnqueueAuthSuccess(42);
        OdooJsonRpcClient client = Build();

        int uid = await client.AuthenticateAsync(TestContext.Current.CancellationToken);

        uid.ShouldBe(42);
        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("POST");
        _handler.Requests[0].Url.ShouldEndWith("/jsonrpc");
        _handler.Requests[0].Body.ShouldContain("authenticate");
        _handler.Requests[0].Body.ShouldContain("testdb");
    }

    [Fact]
    public async Task AuthenticateAsync_SecondCall_UsesCachedUid_NoNewRequest()
    {
        EnqueueAuthSuccess(42);
        OdooJsonRpcClient client = Build();

        await client.AuthenticateAsync(TestContext.Current.CancellationToken);
        int uidAgain = await client.AuthenticateAsync(TestContext.Current.CancellationToken);

        uidAgain.ShouldBe(42);
        _handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AuthenticateAsync_ZeroResult_Throws()
    {
        // Odoo signals auth failure with uid 0 (or null which deserializes to 0 for int).
        _handler.ResponseQueue.Enqueue("""{"result":0}""");
        OdooJsonRpcClient client = Build();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            client.AuthenticateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_ReturnsRecordId_AndPassesValuesInPayload()
    {
        EnqueueAuthSuccess();
        _handler.ResponseQueue.Enqueue("""{"result":7}""");
        OdooJsonRpcClient client = Build();

        int id = await client.CreateAsync(
            "res.partner",
            new Dictionary<string, object?> { ["name"] = "Acme" },
            TestContext.Current.CancellationToken);

        id.ShouldBe(7);
        _handler.Requests.Count.ShouldBe(2); // auth + create
        _handler.Requests[1].Body.ShouldContain("res.partner");
        _handler.Requests[1].Body.ShouldContain("create");
        _handler.Requests[1].Body.ShouldContain("Acme");
    }

    [Fact]
    public async Task ReadAsync_ReturnsResultJson()
    {
        EnqueueAuthSuccess();
        _handler.ResponseQueue.Enqueue("""{"result":[{"state":"posted"}]}""");
        OdooJsonRpcClient client = Build();

        System.Text.Json.JsonElement? result = await client.ReadAsync(
            "account.move", id: 9, fields: ["state"],
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        System.Text.Json.JsonElement value = result.Value;
        value.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task UpdateAsync_PostsWriteRequestForGivenId()
    {
        EnqueueAuthSuccess();
        _handler.ResponseQueue.Enqueue("""{"result":true}""");
        OdooJsonRpcClient client = Build();

        await client.UpdateAsync(
            "res.partner", id: 5,
            new Dictionary<string, object?> { ["name"] = "New" },
            TestContext.Current.CancellationToken);

        _handler.Requests[1].Body.ShouldContain("write");
        _handler.Requests[1].Body.ShouldContain("res.partner");
        _handler.Requests[1].Body.ShouldContain("\"New\"");
    }

    [Fact]
    public async Task RpcError_NonSession_ThrowsInvalidOperationException()
    {
        EnqueueAuthSuccess();
        _handler.ResponseQueue.Enqueue("""{"error":{"message":"Permission denied"}}""");
        OdooJsonRpcClient client = Build();

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            client.CreateAsync("x", new(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Permission denied");
    }

    [Theory]
    [InlineData("Session expired")]
    [InlineData("session_expired")]
    [InlineData("AccessDenied: token invalid")]
    public async Task RpcError_SessionExpired_TriggersReauthAndRetry(string errorMsg)
    {
        EnqueueAuthSuccess(uid: 42);
        // First create call: session-expired error
        _handler.ResponseQueue.Enqueue("{\"error\":{\"message\":\"" + errorMsg + "\"}}");
        // Re-auth call: success with new uid
        _handler.ResponseQueue.Enqueue("""{"result":99}""");
        // Retry of create: success
        _handler.ResponseQueue.Enqueue("""{"result":11}""");
        OdooJsonRpcClient client = Build();

        int id = await client.CreateAsync("res.partner",
            new(),
            TestContext.Current.CancellationToken);

        id.ShouldBe(11);
        _handler.Requests.Count.ShouldBe(4); // auth, create(fail), reauth, create(retry)
    }
}
