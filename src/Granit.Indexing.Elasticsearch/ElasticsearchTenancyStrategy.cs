namespace Granit.Indexing.Elasticsearch;

/// <summary>
/// Multi-tenant index layout strategy for <c>Granit.Indexing.Elasticsearch</c>.
/// </summary>
public enum ElasticsearchTenancyStrategy
{
    /// <summary>
    /// One physical index per <c>TKey</c>. Tenants are isolated by a <c>term</c> filter
    /// on <c>tenant_id</c> applied to every read and write. Cheap on cluster resources;
    /// relies on the framework's filter discipline to keep tenants apart.
    /// </summary>
    Shared,

    /// <summary>
    /// One physical index per <c>(TKey, TenantId)</c> pair. Stricter isolation
    /// (cross-tenant queries are impossible at the cluster level) at the cost of one
    /// extra index per tenant. Recommended for ISO 27001 deployments with hard tenant
    /// separation requirements.
    /// </summary>
    PerTenant,
}
