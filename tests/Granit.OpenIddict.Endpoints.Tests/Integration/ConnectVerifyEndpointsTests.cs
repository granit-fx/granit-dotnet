using System.Net;
using Granit.OpenIddict.Endpoints.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests.Integration;

/// <summary>
/// Tests for <c>ConnectVerifyEndpoints</c> (/connect/verify) that do not require a running
/// OpenIddict server pipeline. When no <c>user_code</c> is present (or the OpenIddict
/// middleware has not processed the request), <c>GetOpenIddictServerRequest()</c> returns
/// <c>null</c> and the endpoint redirects to the configured device verification path.
/// </summary>
public sealed class ConnectVerifyEndpointsTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        _app = builder.Build();
        _app.MapGranitOpenIddictServer();
        await _app.StartAsync(TestContext.Current.CancellationToken);

        _client = _app.GetTestServer().CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Verify_NoUserCode_Get_RedirectsToDefaultDevicePath()
    {
        // GET /connect/verify without a user_code: GetOpenIddictServerRequest() returns null
        // → the endpoint falls into the "no user_code" branch and redirects to /device.
        HttpResponseMessage response = await _client.GetAsync(
            "/connect/verify", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().ShouldBe("/device");
    }

    [Fact]
    public async Task Verify_NoUserCode_Post_RedirectsToDefaultDevicePath()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/connect/verify", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().ShouldBe("/device");
    }

    [Fact]
    public async Task Verify_NoUserCode_CustomDevicePath_RedirectsToCustomPath()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        await using WebApplication app = builder.Build();
        app.MapGranitOpenIddictServer(o => o.DeviceVerificationPath = "/custom-device");
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestServer().CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/connect/verify", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().ShouldBe("/custom-device");
    }
}
