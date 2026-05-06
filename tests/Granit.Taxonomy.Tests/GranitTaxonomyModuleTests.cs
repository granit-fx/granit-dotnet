using Granit.Modularity;
using Granit.Persistence;
using Granit.Taxonomy.Extensions;
using Granit.Taxonomy.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Tests;

public sealed class GranitTaxonomyModuleTests
{
    // -------------------------------------------------------------------------
    // Module metadata
    // -------------------------------------------------------------------------

    [Fact]
    public void Module_DependsOn_GranitPersistenceModule()
    {
        var attributes = (DependsOnAttribute[])Attribute.GetCustomAttributes(
            typeof(GranitTaxonomyModule), typeof(DependsOnAttribute));

        attributes.SelectMany(a => a.DependedTypes)
                  .ShouldContain(typeof(GranitPersistenceModule));
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitTaxonomyModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFrom_GranitModule() =>
        typeof(GranitTaxonomyModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();

    // -------------------------------------------------------------------------
    // AddGranitTaxonomy() — options binding
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitTaxonomy_DoesNotThrow()
    {
        ServiceCollection services = new();
        Should.NotThrow(() => services.AddGranitTaxonomy());
    }

    [Fact]
    public void AddGranitTaxonomy_RegistersOptions()
    {
        ServiceCollection services = new();
        services.AddGranitTaxonomy();

        services.ShouldContain(d => d.ServiceType == typeof(IConfigureOptions<TaxonomyOptions>));
    }

    [Fact]
    public void AddGranitTaxonomy_ResolvedOptions_IsNotNull()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitTaxonomy();

        using ServiceProvider sp = services.BuildServiceProvider();
        TaxonomyOptions options = sp.GetRequiredService<IOptions<TaxonomyOptions>>().Value;

        options.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitTaxonomy_Returns_SameServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitTaxonomy();

        result.ShouldBeSameAs(services);
    }
}
