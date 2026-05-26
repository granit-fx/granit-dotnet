using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Granit.Indexing.Elasticsearch.Internal;

/// <summary>
/// Elasticsearch <see cref="IIndexedDataEraser"/>: runs a single <c>delete_by_query</c>
/// across every per-key index registered for this backend, targeting
/// <c>tenant_id + data_subject_id</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Physical erasure.</b> <c>delete_by_query</c> is a logical delete in Elasticsearch;
/// segments are reclaimed during the next merge or by an explicit <c>forcemerge</c>. The
/// framework documents the merge cadence as an operational concern (default daily) — Art.
/// 17 is satisfied because the data is no longer addressable, but bit-level disposal
/// requires the host's storage policy.
/// </para>
/// <para>
/// <b>Per-tenant fan-out.</b> When the configured strategy is
/// <see cref="ElasticsearchTenancyStrategy.PerTenant"/>, the eraser targets the resolved
/// per-tenant index (or a wildcard pattern when the tenant id is null) so the cascade
/// still works for legacy multi-tenant deletion events that arrive without a pinned tenant.
/// </para>
/// </remarks>
internal sealed class ElasticsearchIndexedDataEraser : IIndexedDataEraser
{
    private readonly ElasticsearchClient _client;
    private readonly IndexNameResolver _indexNames;
    private readonly IReadOnlyList<Type> _keyTypes;

    public ElasticsearchIndexedDataEraser(
        ElasticsearchClient client,
        IndexNameResolver indexNames,
        IReadOnlyList<Type> keyTypes)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(indexNames);
        ArgumentNullException.ThrowIfNull(keyTypes);
        _client = client;
        _indexNames = indexNames;
        _keyTypes = keyTypes;
    }

    public string Name => ElasticsearchIndexer<object>.BackendName;

    public async Task<int> EraseAsync(Guid? tenantId, Guid dataSubjectId, CancellationToken cancellationToken = default)
    {
        int total = 0;

        foreach (Type keyType in _keyTypes)
        {
            string pattern = _indexNames.ResolveSearchPattern(keyType, tenantId);

            Query subjectFilter = new TermQuery
            {
                Field = "dataSubjectId",
                Value = dataSubjectId.ToString(),
            };
            Query tenantFilter = new TermQuery
            {
                Field = "tenantId",
                Value = tenantId?.ToString() ?? string.Empty,
            };
            Query composite = new BoolQuery { Filter = [tenantFilter, subjectFilter] };

            Elastic.Clients.Elasticsearch.DeleteByQueryResponse response = await _client
                .DeleteByQueryAsync(pattern, dq => dq.Query(composite), cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsValidResponse)
            {
                // Missing-index is acceptable — nothing to erase if the host never indexed
                // anything for this TKey under the requested tenant.
                if (response.ElasticsearchServerError?.Error?.Type is "index_not_found_exception")
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Elasticsearch delete_by_query failed on '{pattern}': {response.DebugInformation}");
            }

            total += (int)(response.Deleted ?? 0);
        }

        return total;
    }
}
