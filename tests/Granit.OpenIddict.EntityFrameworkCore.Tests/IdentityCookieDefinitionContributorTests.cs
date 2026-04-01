using Granit.Http.Cookies;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.EntityFrameworkCore.Tests;

public sealed class IdentityCookieDefinitionContributorTests
{
    [Fact]
    public void GetCookieDefinitions_ReturnsThreeIdentityCookies()
    {
        IOptionsMonitor<CookieAuthenticationOptions> monitor = CreateOptionsMonitor();
        IdentityCookieDefinitionContributor sut = new(monitor);

        var definitions = sut.GetCookieDefinitions().ToList();

        definitions.Count.ShouldBe(3);
    }

    [Fact]
    public void GetCookieDefinitions_AllAreStrictlyNecessary()
    {
        IOptionsMonitor<CookieAuthenticationOptions> monitor = CreateOptionsMonitor();
        IdentityCookieDefinitionContributor sut = new(monitor);

        var definitions = sut.GetCookieDefinitions().ToList();

        definitions.ShouldAllBe(d => d.Category == CookieCategory.StrictlyNecessary);
    }

    [Fact]
    public void GetCookieDefinitions_AllAreEssentialAndHttpOnly()
    {
        IOptionsMonitor<CookieAuthenticationOptions> monitor = CreateOptionsMonitor();
        IdentityCookieDefinitionContributor sut = new(monitor);

        var definitions = sut.GetCookieDefinitions().ToList();

        definitions.ShouldAllBe(d => d.IsEssential && d.IsHttpOnly);
    }

    [Fact]
    public void GetCookieDefinitions_UsesDefaultCookieNames()
    {
        IOptionsMonitor<CookieAuthenticationOptions> monitor = CreateOptionsMonitor();
        IdentityCookieDefinitionContributor sut = new(monitor);

        var definitions = sut.GetCookieDefinitions().ToList();

        definitions.Select(d => d.Name).ShouldBe(
        [
            IdentityCookieDefinitionContributor.DefaultApplicationCookieName,
            IdentityCookieDefinitionContributor.DefaultTwoFactorCookieName,
            IdentityCookieDefinitionContributor.DefaultExternalCookieName,
        ]);
    }

    [Fact]
    public void GetCookieDefinitions_RespectsCustomCookieName()
    {
        IOptionsMonitor<CookieAuthenticationOptions> monitor = CreateOptionsMonitor(
            applicationCookieName: "Custom.Auth.Session");
        IdentityCookieDefinitionContributor sut = new(monitor);

        var definitions = sut.GetCookieDefinitions().ToList();

        definitions[0].Name.ShouldBe("Custom.Auth.Session");
    }

    [Fact]
    public void GetCookieDefinitions_NullCookieName_UsesFallback()
    {
        IOptionsMonitor<CookieAuthenticationOptions> monitor = CreateOptionsMonitor(applicationCookieName: null);
        IdentityCookieDefinitionContributor sut = new(monitor);

        var definitions = sut.GetCookieDefinitions().ToList();

        definitions[0].Name.ShouldBe(IdentityCookieDefinitionContributor.DefaultApplicationCookieName);
    }

    private static IOptionsMonitor<CookieAuthenticationOptions> CreateOptionsMonitor(
        string? applicationCookieName = "__Host-id",
        string? twoFactorCookieName = "__Host-id-2fa",
        string? externalCookieName = "__Host-id-ext")
    {
        IOptionsMonitor<CookieAuthenticationOptions> monitor = Substitute.For<IOptionsMonitor<CookieAuthenticationOptions>>();

        monitor.Get(IdentityConstants.ApplicationScheme).Returns(BuildOptions(applicationCookieName));
        monitor.Get(IdentityConstants.TwoFactorUserIdScheme).Returns(BuildOptions(twoFactorCookieName));
        monitor.Get(IdentityConstants.ExternalScheme).Returns(BuildOptions(externalCookieName));

        return monitor;

        static CookieAuthenticationOptions BuildOptions(string? cookieName)
        {
            CookieAuthenticationOptions options = new();
            if (cookieName is not null)
            {
                options.Cookie.Name = cookieName;
            }

            return options;
        }
    }
}
