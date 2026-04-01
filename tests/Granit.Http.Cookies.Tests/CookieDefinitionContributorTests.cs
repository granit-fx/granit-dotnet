using Granit.Http.Cookies.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class CookieDefinitionContributorTests
{
    [Fact]
    public void AddGranitCookies_WithContributor_RegistersContributorCookies()
    {
        ServiceCollection services = new();
        services.AddSingleton<ICookieDefinitionContributor, TestContributor>();
        services.AddGranitCookies(_ => { });

        ServiceProvider provider = services.BuildServiceProvider();
        ICookieRegistry registry = provider.GetRequiredService<ICookieRegistry>();

        registry.IsRegistered("contributor_cookie").ShouldBeTrue();
        registry.GetDefinition("contributor_cookie")!.Category.ShouldBe(CookieCategory.StrictlyNecessary);
    }

    [Fact]
    public void AddGranitCookies_ContributorAndBuilder_MergeBothSources()
    {
        ServiceCollection services = new();
        services.AddSingleton<ICookieDefinitionContributor, TestContributor>();
        services.AddGranitCookies(cookies =>
        {
            cookies.RegisterCookie(new("builder_cookie", CookieCategory.Analytics, 365, false, "From builder"));
        });

        ServiceProvider provider = services.BuildServiceProvider();
        ICookieRegistry registry = provider.GetRequiredService<ICookieRegistry>();

        registry.IsRegistered("contributor_cookie").ShouldBeTrue();
        registry.IsRegistered("builder_cookie").ShouldBeTrue();
        registry.GetAll().Count.ShouldBe(2);
    }

    [Fact]
    public void AddGranitCookies_MultipleContributors_RegistersAll()
    {
        ServiceCollection services = new();
        services.AddSingleton<ICookieDefinitionContributor, TestContributor>();
        services.AddSingleton<ICookieDefinitionContributor, AnotherContributor>();
        services.AddGranitCookies(_ => { });

        ServiceProvider provider = services.BuildServiceProvider();
        ICookieRegistry registry = provider.GetRequiredService<ICookieRegistry>();

        registry.IsRegistered("contributor_cookie").ShouldBeTrue();
        registry.IsRegistered("another_cookie").ShouldBeTrue();
    }

    [Fact]
    public void AddGranitCookies_MultipleCalls_AccumulateDefinitions()
    {
        ServiceCollection services = new();
        services.AddGranitCookies(cookies =>
        {
            cookies.RegisterCookie(new("first", CookieCategory.StrictlyNecessary, 1, true, "First"));
        });
        services.AddGranitCookies(cookies =>
        {
            cookies.RegisterCookie(new("second", CookieCategory.Analytics, 365, false, "Second"));
        });

        ServiceProvider provider = services.BuildServiceProvider();
        ICookieRegistry registry = provider.GetRequiredService<ICookieRegistry>();

        registry.IsRegistered("first").ShouldBeTrue();
        registry.IsRegistered("second").ShouldBeTrue();
    }

    [Fact]
    public void AddGranitCookies_ContributorRegisteredAfterAddGranitCookies_StillDiscovered()
    {
        ServiceCollection services = new();
        services.AddGranitCookies(_ => { });
        services.AddSingleton<ICookieDefinitionContributor, TestContributor>();

        ServiceProvider provider = services.BuildServiceProvider();
        ICookieRegistry registry = provider.GetRequiredService<ICookieRegistry>();

        registry.IsRegistered("contributor_cookie").ShouldBeTrue();
    }

    [Fact]
    public void AddGranitCookies_DuplicateFromContributorAndBuilder_IsIdempotent()
    {
        CookieDefinition shared = new("shared_cookie", CookieCategory.StrictlyNecessary, 1, true, "Shared");

        ServiceCollection services = new();
        services.AddSingleton<ICookieDefinitionContributor>(new StaticContributor(shared));
        services.AddGranitCookies(cookies =>
        {
            cookies.RegisterCookie(shared);
        });

        ServiceProvider provider = services.BuildServiceProvider();
        ICookieRegistry registry = provider.GetRequiredService<ICookieRegistry>();

        registry.IsRegistered("shared_cookie").ShouldBeTrue();
        registry.GetAll().Count.ShouldBe(1);
    }

    private sealed class TestContributor : ICookieDefinitionContributor
    {
        public IEnumerable<CookieDefinition> GetCookieDefinitions()
        {
            yield return new("contributor_cookie", CookieCategory.StrictlyNecessary, 1, true, "From contributor");
        }
    }

    private sealed class AnotherContributor : ICookieDefinitionContributor
    {
        public IEnumerable<CookieDefinition> GetCookieDefinitions()
        {
            yield return new("another_cookie", CookieCategory.Preferences, 180, true, "Another contributor");
        }
    }

    private sealed class StaticContributor(CookieDefinition definition) : ICookieDefinitionContributor
    {
        public IEnumerable<CookieDefinition> GetCookieDefinitions()
        {
            yield return definition;
        }
    }
}
