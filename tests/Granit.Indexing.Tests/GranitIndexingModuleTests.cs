using Granit.Diagnostics;
using Granit.Indexing.Diagnostics;
using Granit.Indexing.Extensions;
using Granit.Indexing.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Tests;

public sealed class GranitIndexingModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitIndexingModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitIndexingModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_depends_on_GranitLanguageDetectionModule()
    {
        // Language detection is a cross-cutting concern; the abstraction lives in its
        // own package so notifications, AI prompts, etc. can consume detection without
        // pulling in indexing. The dependency direction is therefore Indexing →
        // LanguageDetection (one way, never reversed).
        var attrs = (DependsOnAttribute[])typeof(GranitIndexingModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(Granit.LanguageDetection.GranitLanguageDetectionModule));
    }

    [Fact]
    public void ActivitySource_name_is_registered_globally()
    {
        new ServiceCollection().AddGranitIndexing();

        GranitActivitySourceRegistry.GetRegisteredSources().ShouldContain(IndexingActivitySource.Name);
    }

    [Fact]
    public void Default_authorizer_is_null_object()
    {
        ServiceCollection services = [];
        services.AddGranitIndexing();
        ServiceProvider sp = services.BuildServiceProvider();

        ISearchResultAuthorizer<Guid>? authorizer = sp.GetService<ISearchResultAuthorizer<Guid>>();
        authorizer.ShouldNotBeNull();
        authorizer.RecommendedInitialMultiplier.ShouldBe(1);
    }

    [Fact]
    public async Task Default_authorizer_passes_every_candidate_through()
    {
        ServiceCollection services = [];
        services.AddGranitIndexing();
        ISearchResultAuthorizer<Guid> authorizer = services.BuildServiceProvider()
            .GetRequiredService<ISearchResultAuthorizer<Guid>>();

        Guid[] candidates = [Guid.NewGuid(), Guid.NewGuid()];
        AuthorizedResult<Guid> result = await authorizer.FilterAsync(candidates, TestContext.Current.CancellationToken);

        result.Authorized.Count.ShouldBe(2);
    }

    [Fact]
    public void Options_section_name_is_Indexing() =>
        GranitIndexingOptions.SectionName.ShouldBe("Indexing");
}
