using System.Security.Claims;
using Granit.OpenIddict.Extensions;
using Granit.OpenIddict.Services;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Extensions;

public sealed class ClaimsPrincipalDestinationExtensionsTests
{
    [Fact]
    public void SetDestinations_Sets_Destinations_On_All_Claims()
    {
        Claim[] claims =
        [
            new Claim("sub", "user-123"),
            new Claim("email", "alice@test.com"),
        ];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "Test"));

        IClaimsDestinationProvider provider = Substitute.For<IClaimsDestinationProvider>();
        provider.GetDestinations(Arg.Any<Claim>(), Arg.Any<ClaimsPrincipal>())
            .Returns([ClaimsDestinations.AccessToken]);

        ClaimsPrincipal result = ClaimsPrincipalDestinationExtensions.SetDestinations(principal, provider);

        result.ShouldBeSameAs(principal);
        provider.Received(2).GetDestinations(Arg.Any<Claim>(), principal);

        foreach (Claim claim in result.Claims)
        {
            claim.GetDestinations().ShouldContain(ClaimsDestinations.AccessToken);
        }
    }

    [Fact]
    public void SetDestinations_Throws_On_Null_Principal()
    {
        IClaimsDestinationProvider provider = Substitute.For<IClaimsDestinationProvider>();

        Should.Throw<ArgumentNullException>(() =>
            ClaimsPrincipalDestinationExtensions.SetDestinations(null!, provider));
    }

    [Fact]
    public void SetDestinations_Throws_On_Null_Provider()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([], "Test"));

        Should.Throw<ArgumentNullException>(() =>
            ClaimsPrincipalDestinationExtensions.SetDestinations(principal, null!));
    }

    [Fact]
    public void SetDestinations_Returns_Same_Principal()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([], "Test"));
        IClaimsDestinationProvider provider = Substitute.For<IClaimsDestinationProvider>();

        ClaimsPrincipal result = ClaimsPrincipalDestinationExtensions.SetDestinations(principal, provider);

        result.ShouldBeSameAs(principal);
    }

    [Fact]
    public void SetDestinations_With_No_Claims_DoesNotThrow()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([], "Test"));
        IClaimsDestinationProvider provider = Substitute.For<IClaimsDestinationProvider>();

        Should.NotThrow(() => ClaimsPrincipalDestinationExtensions.SetDestinations(principal, provider));

        provider.DidNotReceive().GetDestinations(Arg.Any<Claim>(), Arg.Any<ClaimsPrincipal>());
    }
}
