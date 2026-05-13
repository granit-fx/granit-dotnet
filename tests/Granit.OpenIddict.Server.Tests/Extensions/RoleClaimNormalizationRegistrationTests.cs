using System.Security.Claims;
using Granit.Authentication.Claims;
using Granit.OpenIddict.Server.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Server.Tests.Extensions;

/// <summary>
/// Regression coverage for the self-hosted OpenIddict role-claim normalization bug:
/// <c>UseLocalServer()</c> co-locates the validation handler with the server and
/// returns a <see cref="ClaimsIdentity"/> whose <see cref="ClaimsIdentity.AuthenticationType"/>
/// is the framework default <c>"AuthenticationTypes.Federation"</c> rather than
/// <c>OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme</c>. Before the
/// fix, <see cref="RoleClaimNormalizationTransformation"/> gated out, the short
/// <c>role</c> claim was never promoted to <see cref="ClaimTypes.Role"/>, and every
/// role-gated permission check returned 403.
/// </summary>
public sealed class RoleClaimNormalizationRegistrationTests
{
    private const string FederationScheme = "AuthenticationTypes.Federation";
    private const string OpenIddictValidationScheme = "OpenIddict.Validation.AspNetCore";

    [Fact]
    public void AddGranitOpenIddictServer_registers_federation_scheme_for_self_hosted_principals()
    {
        HostApplicationBuilder builder = BuildMinimalBuilder();

        builder.AddGranitOpenIddictServer();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        RoleClaimNormalizationOptions options = provider
            .GetRequiredService<IOptions<RoleClaimNormalizationOptions>>().Value;

        options.Schemes.ShouldContain(OpenIddictValidationScheme);
        options.Schemes.ShouldContain(FederationScheme);
    }

    [Fact]
    public async Task Transformation_promotes_short_role_claim_on_self_hosted_federation_identity()
    {
        HostApplicationBuilder builder = BuildMinimalBuilder();
        builder.AddGranitOpenIddictServer();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        RoleClaimNormalizationTransformation transformation = scope.ServiceProvider
            .GetServices<IClaimsTransformation>()
            .OfType<RoleClaimNormalizationTransformation>()
            .Single();

        var identity = new ClaimsIdentity(
            claims: [new Claim("role", "admin")],
            authenticationType: FederationScheme);
        var principal = new ClaimsPrincipal(identity);

        ClaimsPrincipal transformed = await transformation.TransformAsync(principal);

        transformed.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldContain("admin");
        transformed.IsInRole("admin").ShouldBeTrue();
    }

    private static HostApplicationBuilder BuildMinimalBuilder()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Environment.EnvironmentName = Environments.Development;
        return builder;
    }
}
