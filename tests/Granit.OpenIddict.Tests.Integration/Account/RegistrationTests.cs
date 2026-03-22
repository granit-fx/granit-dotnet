using System.Net;
using System.Text.Json;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Granit.OpenIddict.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Integration.Account;

[Collection("openiddict-integration")]
public sealed class RegistrationTests(OpenIddictTestApplication app)
{
    [Fact]
    public async Task Should_register_new_user()
    {
        OidcTestClient client = app.CreateOidcClient();
        string uniqueEmail = $"newuser-{Guid.NewGuid():N}@example.com";

        HttpResponseMessage response = await client.RegisterAsync(
            uniqueEmail,
            "V@lidP4ssword!Strong",
            "New",
            "User");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("userId").GetGuid().ShouldNotBe(Guid.Empty);
        doc.RootElement.GetProperty("requiresEmailConfirmation").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_reject_duplicate_email()
    {
        OidcTestClient client = app.CreateOidcClient();
        string uniqueEmail = $"dup-{Guid.NewGuid():N}@example.com";

        // Register first time
        HttpResponseMessage firstResponse = await client.RegisterAsync(
            uniqueEmail,
            "V@lidP4ssword!Strong");
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Register again with same email
        HttpResponseMessage secondResponse = await client.RegisterAsync(
            uniqueEmail,
            "An0therP@ss!Strong");

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Should_reject_weak_password()
    {
        OidcTestClient client = app.CreateOidcClient();
        string uniqueEmail = $"weak-{Guid.NewGuid():N}@example.com";

        HttpResponseMessage response = await client.RegisterAsync(
            uniqueEmail,
            "123");

        // ASP.NET Identity rejects weak passwords. The endpoint may return
        // 422 (validation) or 400 depending on the validator pipeline.
        // The key assertion is that it does NOT succeed.
        ((int)response.StatusCode).ShouldBeInRange(400, 499);
    }
}
