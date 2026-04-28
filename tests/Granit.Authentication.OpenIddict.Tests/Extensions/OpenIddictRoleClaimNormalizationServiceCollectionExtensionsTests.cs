using System.Security.Claims;
using Granit.Authentication.Claims;
using Granit.Authentication.OpenIddict.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.OpenIddict.Tests.Extensions;

public sealed class OpenIddictRoleClaimNormalizationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitOpenIddictRoleClaimNormalization_RegistersTransformation()
    {
        ServiceCollection services = new();

        services.AddGranitOpenIddictRoleClaimNormalization();

        ServiceProvider sp = services.BuildServiceProvider();
        IEnumerable<IClaimsTransformation> transformations = sp.GetServices<IClaimsTransformation>();

        transformations.OfType<RoleClaimNormalizationTransformation>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AddGranitOpenIddictRoleClaimNormalization_AddsOpenIddictValidationSchemeToOptions()
    {
        ServiceCollection services = new();

        services.AddGranitOpenIddictRoleClaimNormalization();

        ServiceProvider sp = services.BuildServiceProvider();
        RoleClaimNormalizationOptions options =
            sp.GetRequiredService<IOptions<RoleClaimNormalizationOptions>>().Value;

        options.Schemes.ShouldContain(
            OpenIddictRoleClaimNormalizationServiceCollectionExtensions.OpenIddictValidationScheme);
    }

    [Fact]
    public void AddGranitOpenIddictRoleClaimNormalization_CalledTwice_DoesNotDuplicateScheme()
    {
        // The Server package and the Authentication.OpenIddict package may both
        // register the OpenIddict scheme in the same composition root. The helper
        // must be idempotent on Schemes.
        ServiceCollection services = new();

        services.AddGranitOpenIddictRoleClaimNormalization();
        services.AddGranitOpenIddictRoleClaimNormalization();

        ServiceProvider sp = services.BuildServiceProvider();
        RoleClaimNormalizationOptions options =
            sp.GetRequiredService<IOptions<RoleClaimNormalizationOptions>>().Value;

        options.Schemes.Count(s =>
            s == OpenIddictRoleClaimNormalizationServiceCollectionExtensions.OpenIddictValidationScheme)
            .ShouldBe(1);
    }

    [Fact]
    public async Task AddGranitOpenIddictRoleClaimNormalization_NormalizesPrincipalsFromOpenIddictScheme()
    {
        // End-to-end: register the helper, resolve the transformation, run it on a
        // principal mimicking what OpenIddict.Validation produces.
        ServiceCollection services = new();
        services.AddGranitOpenIddictRoleClaimNormalization();
        ServiceProvider sp = services.BuildServiceProvider();

        RoleClaimNormalizationTransformation transformation = sp.GetRequiredService<IEnumerable<IClaimsTransformation>>()
            .OfType<RoleClaimNormalizationTransformation>().Single();

        ClaimsIdentity identity = new(
            [new Claim("role", "SuperAdmin")],
            "OpenIddict.Validation.AspNetCore",
            ClaimTypes.NameIdentifier,
            roleType: "role");
        ClaimsPrincipal principal = new(identity);

        ClaimsPrincipal result = await transformation.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["SuperAdmin"]);
    }
}
