using Granit.Bff.Options;
using Shouldly;
using Xunit;

namespace Granit.Bff.Tests.Options;

public sealed class GranitBffOptionsTests
{
    [Fact]
    public void SectionName_IsBff() => GranitBffOptions.SectionName.ShouldBe("Bff");

    [Fact]
    public void DefaultSessionDuration_Is8Hours()
    {
        var options = new GranitBffOptions();

        options.SessionDuration.ShouldBe(TimeSpan.FromHours(8));
    }

    [Fact]
    public void DefaultRefreshGracePeriod_Is1Minute()
    {
        var options = new GranitBffOptions();

        options.RefreshGracePeriod.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void DefaultFrontends_IsEmpty()
    {
        var options = new GranitBffOptions();

        options.Frontends.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultAuthority_IsNull()
    {
        var options = new GranitBffOptions();

        options.Authority.ShouldBeNull();
    }

    [Fact]
    public void SessionDuration_CanBeCustomized()
    {
        var options = new GranitBffOptions { SessionDuration = TimeSpan.FromHours(4) };

        options.SessionDuration.ShouldBe(TimeSpan.FromHours(4));
    }

    [Fact]
    public void RefreshGracePeriod_CanBeCustomized()
    {
        var options = new GranitBffOptions { RefreshGracePeriod = TimeSpan.FromMinutes(5) };

        options.RefreshGracePeriod.ShouldBe(TimeSpan.FromMinutes(5));
    }
}

public sealed class BffFrontendOptionsTests
{
    [Fact]
    public void SessionCookieName_FollowsHostPrefixFormat()
    {
        var frontend = new BffFrontendOptions { Name = "admin" };

        frontend.SessionCookieName.ShouldBe("__Host-granit-bff-admin");
    }

    [Fact]
    public void SessionCookieName_IncludesName()
    {
        var frontend = new BffFrontendOptions { Name = "patient" };

        frontend.SessionCookieName.ShouldBe("__Host-granit-bff-patient");
    }

    [Fact]
    public void EffectivePostLoginRedirectPath_UsesExplicitValue_WhenSet()
    {
        var frontend = new BffFrontendOptions
        {
            PathPrefix = "/admin",
            PostLoginRedirectPath = "/admin/dashboard",
        };

        frontend.EffectivePostLoginRedirectPath.ShouldBe("/admin/dashboard");
    }

    [Fact]
    public void EffectivePostLoginRedirectPath_DefaultsToPathPrefixSlash_WhenPathPrefixSet()
    {
        var frontend = new BffFrontendOptions { PathPrefix = "/admin" };

        frontend.EffectivePostLoginRedirectPath.ShouldBe("/admin/");
    }

    [Fact]
    public void EffectivePostLoginRedirectPath_DefaultsToSlash_WhenPathPrefixEmpty()
    {
        var frontend = new BffFrontendOptions { PathPrefix = string.Empty };

        frontend.EffectivePostLoginRedirectPath.ShouldBe("/");
    }

    [Fact]
    public void EffectivePostLogoutRedirectPath_UsesExplicitValue_WhenSet()
    {
        var frontend = new BffFrontendOptions
        {
            PathPrefix = "/admin",
            PostLogoutRedirectPath = "/admin/goodbye",
        };

        frontend.EffectivePostLogoutRedirectPath.ShouldBe("/admin/goodbye");
    }

    [Fact]
    public void EffectivePostLogoutRedirectPath_DefaultsToPathPrefixSlash_WhenPathPrefixSet()
    {
        var frontend = new BffFrontendOptions { PathPrefix = "/patient" };

        frontend.EffectivePostLogoutRedirectPath.ShouldBe("/patient/");
    }

    [Fact]
    public void EffectivePostLogoutRedirectPath_DefaultsToSlash_WhenPathPrefixEmpty()
    {
        var frontend = new BffFrontendOptions { PathPrefix = string.Empty };

        frontend.EffectivePostLogoutRedirectPath.ShouldBe("/");
    }

    [Fact]
    public void DefaultScopes_ContainsExpectedOidcScopes()
    {
        var frontend = new BffFrontendOptions();

        frontend.Scopes.ShouldContain("openid");
        frontend.Scopes.ShouldContain("profile");
        frontend.Scopes.ShouldContain("email");
        frontend.Scopes.ShouldContain("roles");
        frontend.Scopes.ShouldContain("offline_access");
    }

    [Fact]
    public void DefaultName_IsEmpty()
    {
        var frontend = new BffFrontendOptions();

        frontend.Name.ShouldBe(string.Empty);
    }

    [Fact]
    public void DefaultClientId_IsEmpty()
    {
        var frontend = new BffFrontendOptions();

        frontend.ClientId.ShouldBe(string.Empty);
    }

    [Fact]
    public void DefaultClientSecret_IsEmpty()
    {
        var frontend = new BffFrontendOptions();

        frontend.ClientSecret.ShouldBe(string.Empty);
    }

    [Fact]
    public void DefaultPathPrefix_IsEmpty()
    {
        var frontend = new BffFrontendOptions();

        frontend.PathPrefix.ShouldBe(string.Empty);
    }

    [Fact]
    public void DefaultStaticFilesPath_IsEmpty()
    {
        var frontend = new BffFrontendOptions();

        frontend.StaticFilesPath.ShouldBe(string.Empty);
    }

    [Fact]
    public void DefaultPostLoginRedirectPath_IsNull()
    {
        var frontend = new BffFrontendOptions();

        frontend.PostLoginRedirectPath.ShouldBeNull();
    }

    [Fact]
    public void DefaultPostLogoutRedirectPath_IsNull()
    {
        var frontend = new BffFrontendOptions();

        frontend.PostLogoutRedirectPath.ShouldBeNull();
    }

    [Fact]
    public void DefaultErrorRedirectPath_IsNull()
    {
        var frontend = new BffFrontendOptions();

        frontend.ErrorRedirectPath.ShouldBeNull();
    }

    [Fact]
    public void EffectiveErrorRedirectPath_UsesExplicitValue_WhenSet()
    {
        var frontend = new BffFrontendOptions
        {
            PathPrefix = "/admin",
            ErrorRedirectPath = "/admin/error",
        };

        frontend.EffectiveErrorRedirectPath.ShouldBe("/admin/error");
    }

    [Fact]
    public void EffectiveErrorRedirectPath_DefaultsToPathPrefixLogin_WhenPathPrefixSet()
    {
        var frontend = new BffFrontendOptions { PathPrefix = "/admin" };

        frontend.EffectiveErrorRedirectPath.ShouldBe("/admin/login");
    }

    [Fact]
    public void EffectiveErrorRedirectPath_DefaultsToLogin_WhenPathPrefixEmpty()
    {
        var frontend = new BffFrontendOptions { PathPrefix = string.Empty };

        frontend.EffectiveErrorRedirectPath.ShouldBe("/login");
    }
}
