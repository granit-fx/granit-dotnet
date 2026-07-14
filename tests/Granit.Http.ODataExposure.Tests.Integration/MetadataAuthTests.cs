using System.Net;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// #3005 — the <c>$metadata</c> / service-document authorization stance at
/// the HTTP level. Anonymous mount: both discovery documents are reachable
/// without credentials (the declared public-feed scenario). Gated mount:
/// unauthenticated → 401 (challenge from <c>RequireAuthorization()</c>),
/// authenticated without the permission → 403 (endpoint filter +
/// <c>IPermissionChecker</c>), authenticated with it → 200. Entity routes
/// are wired OUTSIDE the metadata group and keep their own per-set gates.
/// </summary>
public sealed class MetadataAuthTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string MetadataPermission = "OData.Test.Metadata.Read";

    private readonly PostgresFixture _postgres = postgres;

    private ODataTestApp _appAnonymousMetadata = null!;
    private ODataTestApp _appGatedGranted = null!;
    private ODataTestApp _appGatedDenied = null!;

    public async ValueTask InitializeAsync()
    {
        // Default stance in ODataTestApp is AllowAnonymousMetadata().
        _appAnonymousMetadata = await ODataTestApp.CreateAsync(_postgres.ConnectionString);

        _appGatedGranted = await ODataTestApp.CreateAsync(
            _postgres.ConnectionString,
            configureEntitySet: null,
            rateLimitPermitLimit: null,
            configureOptions: opts => opts.RequireMetadataPermission(MetadataPermission));

        _appGatedDenied = await ODataTestApp.CreateAsync(
            _postgres.ConnectionString,
            configureEntitySet: null,
            rateLimitPermitLimit: null,
            configureOptions: opts => opts.RequireMetadataPermission(MetadataPermission),
            permissionPredicate: permission => permission != MetadataPermission);
    }

    public async ValueTask DisposeAsync()
    {
        await _appAnonymousMetadata.DisposeAsync();
        await _appGatedGranted.DisposeAsync();
        await _appGatedDenied.DisposeAsync();
    }

    [Theory]
    [InlineData("/api/granit/odata/$metadata")]
    [InlineData("/api/granit/odata")]
    public async Task AnonymousMount_DiscoveryDocuments_Reachable_Unauthenticated(string url)
    {
        HttpResponseMessage response = await SendAnonymousAsync(_appAnonymousMetadata, url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/granit/odata/$metadata")]
    [InlineData("/api/granit/odata")]
    public async Task GatedMount_Unauthenticated_Returns401(string url)
    {
        HttpResponseMessage response = await SendAnonymousAsync(_appGatedGranted, url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/granit/odata/$metadata")]
    [InlineData("/api/granit/odata")]
    public async Task GatedMount_AuthenticatedWithoutPermission_Returns403(string url)
    {
        HttpResponseMessage response = await SendAuthenticatedAsync(_appGatedDenied, url);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/granit/odata/$metadata")]
    [InlineData("/api/granit/odata")]
    public async Task GatedMount_AuthenticatedWithPermission_Returns200(string url)
    {
        HttpResponseMessage response = await SendAuthenticatedAsync(_appGatedGranted, url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GatedMount_MetadataDocument_StillListsEntitySet_WhenAuthorized()
    {
        HttpResponseMessage response = await SendAuthenticatedAsync(
            _appGatedGranted, "/api/granit/odata/$metadata");

        string csdl = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        csdl.ShouldContain("Invoices");
    }

    [Fact]
    public async Task GatedMount_EntityRoute_KeepsItsOwnAnonymousGate()
    {
        // The metadata stance wraps ONLY the discovery documents. The
        // Invoices set declared AllowAnonymousAccess() and must stay
        // reachable without credentials even when $metadata is gated.
        HttpResponseMessage response = await SendAnonymousAsync(
            _appGatedDenied, "/api/granit/odata/Invoices");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> SendAnonymousAsync(ODataTestApp app, string url)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        return await app.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> SendAuthenticatedAsync(ODataTestApp app, string url)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, "compliance-officer");
        return await app.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
