using System.Net;
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

        // Anti-enumeration: always 202 regardless of outcome (VULN-201)
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Should_return_accepted_for_duplicate_email()
    {
        OidcTestClient client = app.CreateOidcClient();
        string uniqueEmail = $"dup-{Guid.NewGuid():N}@example.com";

        // Register first time
        HttpResponseMessage firstResponse = await client.RegisterAsync(
            uniqueEmail,
            "V@lidP4ssword!Strong");
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Register again with same email — same 202 to prevent enumeration
        HttpResponseMessage secondResponse = await client.RegisterAsync(
            uniqueEmail,
            "An0therP@ss!Strong");

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
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
