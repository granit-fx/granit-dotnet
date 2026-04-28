using System.Security.Claims;
using Granit.Authentication.Claims;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.Tests.Claims;

public sealed class RoleClaimNormalizationTransformationTests
{
    private const string Scheme = "OpenIddict.Validation.AspNetCore";

    [Fact]
    public async Task TransformAsync_WithMatchingScheme_CopiesShortRoleClaimsToClaimTypesRole()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            Scheme,
            new Claim("role", "SuperAdmin"),
            new Claim("role", "Auditor"));

        RoleClaimNormalizationTransformation sut = CreateSut(opts => opts.Schemes.Add(Scheme));

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["SuperAdmin", "Auditor"]);
        result.IsInRole("SuperAdmin").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithSchemeNotInList_LeavesPrincipalUnchanged()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            "Cookies",
            new Claim("role", "X"));

        RoleClaimNormalizationTransformation sut = CreateSut(opts => opts.Schemes.Add(Scheme));

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithEmptySchemesList_IsNoOp()
    {
        // Safe default: never silently mutate principals when no scheme is configured.
        ClaimsPrincipal principal = CreatePrincipal(
            Scheme,
            new Claim("role", "X"));

        RoleClaimNormalizationTransformation sut = CreateSut(_ => { });

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithMultipleSchemes_MatchesAnyConfiguredScheme()
    {
        ClaimsPrincipal openIddictPrincipal = CreatePrincipal(Scheme, new Claim("role", "A"));
        ClaimsPrincipal customPrincipal = CreatePrincipal("CustomBearer", new Claim("role", "B"));

        RoleClaimNormalizationTransformation sut = CreateSut(opts =>
        {
            opts.Schemes.Add(Scheme);
            opts.Schemes.Add("CustomBearer");
        });

        ClaimsPrincipal r1 = await sut.TransformAsync(openIddictPrincipal);
        ClaimsPrincipal r2 = await sut.TransformAsync(customPrincipal);

        r1.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["A"]);
        r2.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["B"]);
    }

    [Fact]
    public async Task TransformAsync_WithCustomSourceClaimTypes_NormalizesAllConfiguredTypes()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            Scheme,
            new Claim("role", "FromRole"),
            new Claim("groups", "FromGroups"));

        RoleClaimNormalizationTransformation sut = CreateSut(opts =>
        {
            opts.Schemes.Add(Scheme);
            opts.SourceClaimTypes = ["role", "groups"];
        });

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .ShouldBe(["FromRole", "FromGroups"], ignoreOrder: true);
    }

    [Fact]
    public async Task TransformAsync_WithClaimTypesRoleAsSourceType_DoesNotInfiniteLoop()
    {
        // Edge case: if a consumer accidentally lists ClaimTypes.Role as a source, we
        // skip it (otherwise we'd duplicate every existing ClaimTypes.Role claim).
        ClaimsPrincipal principal = CreatePrincipal(
            Scheme,
            new Claim(ClaimTypes.Role, "Existing"));

        RoleClaimNormalizationTransformation sut = CreateSut(opts =>
        {
            opts.Schemes.Add(Scheme);
            opts.SourceClaimTypes = ["role", ClaimTypes.Role];
        });

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).Count(c => c.Value == "Existing").ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_IsIdempotent_AcrossMultipleInvocations()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            Scheme,
            new Claim("role", "SuperAdmin"));

        RoleClaimNormalizationTransformation sut = CreateSut(opts => opts.Schemes.Add(Scheme));

        await sut.TransformAsync(principal);
        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).Count(c => c.Value == "SuperAdmin").ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_WhenClaimTypesRoleAlreadyPresent_DoesNotDuplicate()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            Scheme,
            new Claim("role", "Admin"),
            new Claim(ClaimTypes.Role, "Admin"));

        RoleClaimNormalizationTransformation sut = CreateSut(opts => opts.Schemes.Add(Scheme));

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).Count(c => c.Value == "Admin").ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_WithUnauthenticatedIdentity_LeavesPrincipalUnchanged()
    {
        ClaimsIdentity identity = new();
        ClaimsPrincipal principal = new(identity);

        RoleClaimNormalizationTransformation sut = CreateSut(opts => opts.Schemes.Add(Scheme));

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithNullPrincipal_Throws()
    {
        RoleClaimNormalizationTransformation sut = CreateSut(opts => opts.Schemes.Add(Scheme));

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.TransformAsync(null!));
    }

    [Fact]
    public async Task TransformAsync_WithNoSourceClaims_DoesNotMutate()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            Scheme,
            new Claim("sub", "user-123"),
            new Claim("email", "u@example.com"));

        RoleClaimNormalizationTransformation sut = CreateSut(opts => opts.Schemes.Add(Scheme));

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    private static RoleClaimNormalizationTransformation CreateSut(
        Action<RoleClaimNormalizationOptions> configure)
    {
        RoleClaimNormalizationOptions options = new();
        configure(options);
        return new RoleClaimNormalizationTransformation(Options.Create(options));
    }

    private static ClaimsPrincipal CreatePrincipal(string authenticationType, params Claim[] claims)
    {
        ClaimsIdentity identity = new(claims, authenticationType, ClaimTypes.NameIdentifier, roleType: "role");
        return new ClaimsPrincipal(identity);
    }
}
