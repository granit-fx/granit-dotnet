using Granit.Indexing.Elasticsearch.Options;

namespace Granit.Indexing.Elasticsearch.Internal;

/// <summary>
/// Resolves Elasticsearch index names from <c>(TKey, tenantId)</c> tuples per the
/// configured tenancy strategy.
/// </summary>
/// <remarks>
/// Final names are lowercased — ES rejects index names with uppercase characters. The
/// per-tenant suffix is the <c>N</c>-format Guid (32 hex chars, no separators) so the
/// resulting name fits ES's 255-byte limit comfortably.
/// </remarks>
internal sealed class IndexNameResolver
{
    private readonly IndexingElasticsearchOptions _options;

    public IndexNameResolver(IndexingElasticsearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public string Resolve(Type keyType, Guid? tenantId)
    {
        ArgumentNullException.ThrowIfNull(keyType);

        string baseName = $"{_options.IndexPrefix}-{keyType.Name}".ToLowerInvariant();

        return _options.Strategy switch
        {
            ElasticsearchTenancyStrategy.PerTenant when tenantId.HasValue =>
                $"{baseName}-{tenantId.Value:N}",
            _ => baseName,
        };
    }

    /// <summary>
    /// Wildcard pattern used to enumerate every per-key index across tenants — used by
    /// the GDPR eraser to fan out a single <c>delete_by_query</c> across all per-tenant
    /// shards.
    /// </summary>
    public string ResolveSearchPattern(Type keyType, Guid? tenantId) =>
        _options.Strategy switch
        {
            ElasticsearchTenancyStrategy.PerTenant when tenantId.HasValue => Resolve(keyType, tenantId),
            ElasticsearchTenancyStrategy.PerTenant => $"{_options.IndexPrefix}-{keyType.Name}-*".ToLowerInvariant(),
            _ => Resolve(keyType, tenantId: null),
        };
}
