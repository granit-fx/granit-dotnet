using Granit.Indexing.Elasticsearch.Internal;
using Granit.Indexing.Elasticsearch.Options;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Elasticsearch.Tests;

public sealed class IndexNameResolverTests
{
    private static readonly Guid TenantA = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Shared_strategy_returns_a_single_index_name_per_key_type()
    {
        IndexNameResolver resolver = new(new IndexingElasticsearchOptions { Strategy = ElasticsearchTenancyStrategy.Shared });

        resolver.Resolve(typeof(Guid), TenantA).ShouldBe("granit-indexing-guid");
        resolver.Resolve(typeof(Guid), tenantId: null).ShouldBe("granit-indexing-guid");
    }

    [Fact]
    public void PerTenant_strategy_returns_a_distinct_index_name_per_tenant()
    {
        IndexNameResolver resolver = new(new IndexingElasticsearchOptions { Strategy = ElasticsearchTenancyStrategy.PerTenant });

        string nameA = resolver.Resolve(typeof(Guid), TenantA);
        string nameNull = resolver.Resolve(typeof(Guid), tenantId: null);

        nameA.ShouldStartWith("granit-indexing-guid-");
        nameA.ShouldEndWith(TenantA.ToString("N"));
        nameNull.ShouldBe("granit-indexing-guid");
    }

    [Fact]
    public void Resolve_lowercases_the_index_name()
    {
        // ES rejects uppercase characters in index names; resolver MUST normalise the
        // key-type name (which is PascalCase) before composing the index path.
        IndexNameResolver resolver = new(new IndexingElasticsearchOptions
        {
            IndexPrefix = "Acme-Search",
            Strategy = ElasticsearchTenancyStrategy.Shared,
        });

        string name = resolver.Resolve(typeof(Guid), TenantA);

        name.ShouldBe(name.ToLowerInvariant());
    }

    [Fact]
    public void Search_pattern_for_PerTenant_without_tenant_returns_wildcard()
    {
        // The GDPR eraser may receive a deletion event with no pinned tenant; in that
        // case the per-tenant fan-out walks every tenant's index via a wildcard pattern.
        IndexNameResolver resolver = new(new IndexingElasticsearchOptions { Strategy = ElasticsearchTenancyStrategy.PerTenant });

        string pattern = resolver.ResolveSearchPattern(typeof(Guid), tenantId: null);

        pattern.ShouldBe("granit-indexing-guid-*");
    }

    [Fact]
    public void Search_pattern_for_Shared_strategy_ignores_tenant_id()
    {
        IndexNameResolver resolver = new(new IndexingElasticsearchOptions { Strategy = ElasticsearchTenancyStrategy.Shared });

        resolver.ResolveSearchPattern(typeof(Guid), TenantA).ShouldBe("granit-indexing-guid");
        resolver.ResolveSearchPattern(typeof(Guid), tenantId: null).ShouldBe("granit-indexing-guid");
    }
}
